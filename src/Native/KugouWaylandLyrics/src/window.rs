use crate::chrome::Chrome;
use crate::interaction::Interaction;
use crate::protocol::{self, Event, PlaybackClock, State};
use crate::render::{self, Geometry, Rect};
use gtk::{cairo, gdk, glib, prelude::*};
use gtk4_layer_shell::{Edge, KeyboardMode, Layer, LayerShell};
use std::{
    cell::{Cell, RefCell},
    io::{BufRead, Read},
    rc::Rc,
    sync::{Arc, Mutex},
    time::Duration,
};

#[derive(Default)]
struct Inbox {
    latest: Option<State>,
    closed: bool,
}

struct Model {
    state: State,
    clock: PlaybackClock,
    interaction: Interaction,
    ready: bool,
    received_state: bool,
    position_initialized: bool,
    map_revision: u64,
    input_rects: Vec<(i32, i32, i32, i32)>,
}

impl Model {
    fn geometry(&self, width: i32, height: i32) -> Geometry {
        render::geometry(
            &self.state,
            width,
            height,
            self.interaction.x,
            self.interaction.y,
        )
    }
}

pub fn run() -> Result<(), Box<dyn std::error::Error>> {
    gtk::init()?;
    if !gtk4_layer_shell::is_supported() {
        return Err("compositor does not advertise layer-shell".into());
    }
    let main_loop = glib::MainLoop::new(None, false);
    let display = gdk::Display::default().ok_or("no Wayland display")?;
    crate::chrome::install_css(&display);
    let window = gtk::Window::builder()
        .title("Kugou Wayland Lyrics")
        .decorated(false)
        .build();
    window.add_css_class("kugou-wayland-lyrics");
    window.init_layer_shell();
    window.set_namespace(Some("kugou-desktop-lyrics"));
    window.set_layer(Layer::Top);
    window.set_keyboard_mode(KeyboardMode::None);
    window.set_exclusive_zone(-1);
    for edge in [Edge::Top, Edge::Right, Edge::Bottom, Edge::Left] {
        window.set_anchor(edge, true);
    }
    let area = gtk::DrawingArea::new();
    let state = State::default();
    let model = Rc::new(RefCell::new(Model {
        clock: PlaybackClock::new(&state),
        state,
        interaction: Interaction::new(0, 0),
        ready: false,
        received_state: false,
        position_initialized: false,
        map_revision: 0,
        input_rects: Vec::new(),
    }));
    let action_model = model.clone();
    let action_window = window.clone();
    let action_loop = main_loop.clone();
    let action_area = area.clone();
    let chrome = Rc::new(Chrome::new(&area, move |action| {
        handle_action(&action_model, &action_window, &action_loop, action);
        action_area.queue_draw();
    }));
    let root = chrome.root.clone();
    window.set_child(Some(&root));
    let inbox = Arc::new(Mutex::new(Inbox::default()));
    read_stdin(inbox.clone());

    let draw_model = model.clone();
    let draw_root = root.clone();
    area.set_draw_func(move |_, cr, _, _| {
        let model = draw_model.borrow();
        // This canvas is transparent and sits ABOVE GTK's glass background.
        // Painting with CLEAR here would erase the panel from the snapshot.
        let geometry = model.geometry(draw_root.width(), draw_root.height());
        let _ = cr.save();
        cr.translate(-geometry.content.x, -geometry.content.y);
        render::draw(cr, &model.state, model.clock.position_ms(), &geometry);
        let _ = cr.restore();
    });

    install_drag(&root, &chrome.glass, &area, &model, &window);

    let poll_inbox = inbox.clone();
    let poll_loop = main_loop.clone();
    let poll_area = area.clone();
    let poll_source = glib::timeout_add_local(Duration::from_millis(50), move || {
        let inbox = poll_inbox.lock().unwrap();
        if inbox.closed {
            poll_loop.quit();
        }
        if inbox.latest.is_some() {
            poll_area.queue_draw();
        }
        glib::ControlFlow::Continue
    });
    let map_revision = Rc::new(Cell::new(0_u64));
    let map_revision_signal = map_revision.clone();
    window.connect_realize(|window| {
        if let Some(surface) = window.surface() {
            surface.set_input_region(Some(&cairo::Region::create()));
        }
    });
    window.connect_map(move |_| map_revision_signal.set(map_revision_signal.get().wrapping_add(1)));
    let tick_model = model.clone();
    let tick_window = window.clone();
    let tick_loop = main_loop.clone();
    root.add_tick_callback(move |root, _| {
        let mut inbox = inbox.lock().unwrap();
        if inbox.closed {
            tick_loop.quit();
            return glib::ControlFlow::Break;
        }
        let incoming = inbox.latest.take();
        drop(inbox);
        let width = root.width();
        let height = root.height();
        // GTK mutations can synchronously emit gesture cancellation and button
        // callbacks. Calculate changes under a short borrow; apply ALL widget
        // and surface changes only after releasing the model.
        let (ui_state, geometry, input_regions, ready, layer, monitor, redraw) = {
            let mut model = tick_model.borrow_mut();
            let mut changed = false;
            let mut placement_changed = false;
            let mut layer = None;
            let mut monitor = None;
            if let Some(mut state) = incoming {
                changed = true;
                model.received_state = true;
                model.interaction.reconcile(&mut state);
                placement_changed = state.placement != model.state.placement
                    || state.vertical != model.state.vertical;
                if state.over_fullscreen != model.state.over_fullscreen {
                    layer = Some(if state.over_fullscreen {
                        Layer::Overlay
                    } else {
                        Layer::Top
                    });
                }
                model.clock = PlaybackClock::new(&state);
                model.state = state;
            }
            if model.map_revision != map_revision.get() {
                model.map_revision = map_revision.get();
                model.input_rects.clear();
            }
            if !model.received_state || width <= 0 || height <= 0 {
                return glib::ControlFlow::Continue;
            }
            if (placement_changed || !model.position_initialized)
                && model.interaction.drag_origin.is_none()
            {
                if model.state.placement.has_value {
                    model.interaction.x = model.state.placement.x;
                    model.interaction.y = model.state.placement.y;
                    monitor = Some(model.state.placement.monitor.clone());
                } else {
                    model.interaction.x = if model.state.vertical {
                        width - 280
                    } else {
                        (width - 1024) / 2
                    };
                    model.interaction.y = if model.state.vertical {
                        80
                    } else {
                        height - 240
                    };
                }
                model.position_initialized = true;
            }
            let geometry = model.geometry(width, height);
            let regions = if model.state.locked {
                vec![geometry.hotspot]
            } else {
                vec![geometry.content, geometry.controls]
            };
            let regions = input_rectangles(&regions);
            let input_changed = regions != model.input_rects;
            let input_regions = input_changed.then(|| {
                model.input_rects = regions.clone();
                regions
            });
            let ready = !model.ready;
            model.ready = true;
            let ui_state = (changed || input_changed || ready).then(|| model.state.clone());
            (
                ui_state,
                geometry,
                input_regions,
                ready,
                layer,
                monitor,
                changed || input_changed || model.state.playing,
            )
        };
        if let Some(layer) = layer {
            tick_window.set_layer(layer);
        }
        if let Some(monitor) = monitor {
            select_monitor(&tick_window, &monitor);
        }
        if let Some(state) = ui_state {
            chrome.update(&state, &geometry);
        }
        if let Some(regions) = input_regions {
            apply_input_region(&tick_window, &regions);
        }
        if ready {
            protocol::emit(Event::Ready {
                version: protocol::VERSION,
            });
        }
        if redraw {
            area.queue_draw();
        }
        glib::ControlFlow::Continue
    });

    let close_loop = main_loop.clone();
    window.connect_close_request(move |_| {
        close_loop.quit();
        glib::Propagation::Proceed
    });
    window.present();
    main_loop.run();
    poll_source.remove();
    window.destroy();
    Ok(())
}

fn install_drag(
    root: &gtk::Overlay,
    glass: &gtk::Box,
    area: &gtk::DrawingArea,
    model: &Rc<RefCell<Model>>,
    window: &gtk::Window,
) -> gtk::GestureDrag {
    // One drag gesture in stable output coordinates; native GTK buttons own
    // their click gestures. Never arbitrate a click and drag on the same canvas.
    let drag = gtk::GestureDrag::new();
    drag.set_button(1);
    let drag_model = model.clone();
    let drag_root = root.clone();
    let drag_glass = glass.clone();
    drag.connect_drag_begin(move |gesture, x, y| {
        let origin = {
            let model = drag_model.borrow();
            let geometry = model.geometry(drag_root.width(), drag_root.height());
            if model.state.locked
                || !geometry.content.contains(x, y)
                || geometry.controls.contains(x, y)
            {
                None
            } else {
                Some((geometry.content.x as i32, geometry.content.y as i32))
            }
        };
        if let Some(origin) = origin {
            drag_model.borrow_mut().interaction.drag_origin = Some(origin);
            gesture.set_state(gtk::EventSequenceState::Claimed);
            drag_glass.set_cursor_from_name(Some("grabbing"));
        } else {
            // This emits ::cancel synchronously for an active pointer sequence.
            // The cancel handler is allowed to borrow the model again.
            gesture.set_state(gtk::EventSequenceState::Denied);
        }
    });

    let drag_model = model.clone();
    let drag_area = area.clone();
    drag.connect_drag_update(move |_, dx, dy| {
        let mut model = drag_model.borrow_mut();
        if let Some((x, y)) = model.interaction.drag_origin {
            model.interaction.x = x.saturating_add(dx as i32);
            model.interaction.y = y.saturating_add(dy as i32);
            drop(model);
            drag_area.queue_draw();
        }
    });
    let drag_model = model.clone();
    let drag_root = root.clone();
    let drag_window = window.clone();
    let drag_glass = glass.clone();
    drag.connect_drag_end(move |_, _, _| {
        let mut model = drag_model.borrow_mut();
        if model.interaction.drag_origin.is_some() {
            let geometry = model.geometry(drag_root.width(), drag_root.height());
            record_placement(&mut model, &drag_window, &geometry);
        }
        drop(model);
        drag_glass.set_cursor_from_name(Some("grab"));
    });
    let cancel_model = model.clone();
    let cancel_glass = glass.clone();
    drag.connect_cancel(move |_, _| {
        cancel_model.borrow_mut().interaction.drag_origin = None;
        cancel_glass.set_cursor_from_name(Some("grab"));
    });
    root.add_controller(drag.clone());
    drag
}

fn handle_action(
    model: &Rc<RefCell<Model>>,
    window: &gtk::Window,
    main_loop: &glib::MainLoop,
    action: &str,
) {
    match action {
        "close" => {
            protocol::emit(Event::Action { name: "close" });
            main_loop.quit();
        }
        "lock" | "unlock" => {
            {
                let mut model = model.borrow_mut();
                let Model {
                    interaction, state, ..
                } = &mut *model;
                interaction.set_locked(state, action == "lock");
            }
            protocol::emit(Event::Action { name: action });
        }
        "monitor" => {
            let display = WidgetExt::display(window);
            let monitors = display.monitors();
            let current = window
                .surface()
                .and_then(|s| display.monitor_at_surface(&s));
            let index = (0..monitors.n_items())
                .find(|i| {
                    monitors
                        .item(*i)
                        .and_then(|m| m.downcast::<gdk::Monitor>().ok())
                        == current
                })
                .unwrap_or(0);
            if monitors.n_items() > 1
                && let Some(next) = monitors
                    .item((index + 1) % monitors.n_items())
                    .and_then(|m| m.downcast::<gdk::Monitor>().ok())
            {
                window.set_monitor(Some(&next));
                let mut model = model.borrow_mut();
                model.input_rects.clear();
                let geometry = model.geometry(window.width(), window.height());
                record_placement(&mut model, window, &geometry);
            }
        }
        _ => protocol::emit(Event::Action { name: action }),
    }
}

fn record_placement(model: &mut Model, window: &gtk::Window, geometry: &Geometry) {
    let monitor = window
        .monitor()
        .or_else(|| {
            window
                .surface()
                .and_then(|s| WidgetExt::display(window).monitor_at_surface(&s))
        })
        .and_then(|m| m.connector())
        .map(|v| v.to_string())
        .unwrap_or_default();
    model.interaction.finish_drag(
        &mut model.state,
        geometry.content.x as i32,
        geometry.content.y as i32,
        monitor,
    );
    protocol::emit(Event::Placement {
        vertical: model.state.vertical,
        placement: &model.state.placement,
    });
}

fn select_monitor(window: &gtk::Window, connector: &str) {
    let monitors = WidgetExt::display(window).monitors();
    for i in 0..monitors.n_items() {
        if let Some(monitor) = monitors
            .item(i)
            .and_then(|m| m.downcast::<gdk::Monitor>().ok())
            && monitor.connector().as_deref() == Some(connector)
        {
            window.set_monitor(Some(&monitor));
            break;
        }
    }
}

fn input_rectangles(rects: &[Rect]) -> Vec<(i32, i32, i32, i32)> {
    rects
        .iter()
        .map(|r| {
            (
                r.x.floor() as i32,
                r.y.floor() as i32,
                r.width.ceil() as i32,
                r.height.ceil() as i32,
            )
        })
        .collect()
}

fn apply_input_region(window: &gtk::Window, rects: &[(i32, i32, i32, i32)]) {
    if let Some(surface) = window.surface() {
        let region = cairo::Region::create();
        for &(x, y, width, height) in rects {
            let _ = region.union_rectangle(&cairo::RectangleInt::new(x, y, width, height));
        }
        surface.set_input_region(Some(&region));
    }
}

fn read_stdin(inbox: Arc<Mutex<Inbox>>) {
    std::thread::spawn(move || {
        let mut reader = std::io::BufReader::new(std::io::stdin());
        loop {
            let mut bytes = Vec::new();
            let result = reader
                .by_ref()
                .take(protocol::MAX_MESSAGE_BYTES as u64 + 1)
                .read_until(b'\n', &mut bytes);
            match result {
                Ok(0) | Err(_) => break,
                Ok(_) if bytes.len() > protocol::MAX_MESSAGE_BYTES => {
                    eprintln!("Wayland lyrics: message too large");
                    break;
                }
                _ => match serde_json::from_slice::<State>(&bytes) {
                    Ok(mut state) => match state.validate() {
                        Ok(()) => inbox.lock().unwrap().latest = Some(state),
                        Err(error) => {
                            eprintln!("Wayland lyrics: {error}");
                            break;
                        }
                    },
                    Err(error) => {
                        eprintln!("Wayland lyrics: invalid state: {error}");
                        break;
                    }
                },
            }
        }
        inbox.lock().unwrap().closed = true;
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    fn settle() {
        let deadline = std::time::Instant::now() + Duration::from_millis(260);
        let context = glib::MainContext::default();
        while std::time::Instant::now() < deadline {
            while context.pending() {
                context.iteration(false);
            }
            std::thread::sleep(Duration::from_millis(4));
        }
    }

    fn snapshot(window: &gtk::Window, name: &str) {
        let Ok(directory) = std::env::var("KUGOU_WAYLAND_PREVIEW_DIR") else {
            return;
        };
        std::fs::create_dir_all(&directory).unwrap();
        let paintable = gtk::WidgetPaintable::new(Some(window));
        let snapshot = gtk::Snapshot::new();
        paintable.snapshot(&snapshot, window.width() as f64, window.height() as f64);
        let node = snapshot.to_node().expect("native widget snapshot");
        let texture = window.renderer().unwrap().render_texture(&node, None);
        texture
            .save_to_png(std::path::Path::new(&directory).join(format!("{name}.png")))
            .unwrap();
    }

    #[test]
    #[ignore = "opens a native layer-shell test window; run explicitly in a Wayland session"]
    fn native_unlock_buttons_and_drag_use_real_widget_layout() {
        gtk::test_synced(|| {
            let display = gdk::Display::default().unwrap();
            crate::chrome::install_css(&display);
            assert!(gtk4_layer_shell::is_supported());
            let window = gtk::Window::builder()
                .default_width(1280)
                .default_height(720)
                .decorated(false)
                .build();
            window.add_css_class("kugou-wayland-lyrics");
            window.init_layer_shell();
            window.set_namespace(Some("kugou-lyrics-interaction-test"));
            window.set_layer(Layer::Top);
            window.set_anchor(Edge::Top, true);
            window.set_anchor(Edge::Left, true);
            window.set_keyboard_mode(KeyboardMode::None);
            let area = gtk::DrawingArea::new();
            let state = State {
                locked: true,
                current: Some(protocol::Line {
                    text: "你好，Wayland 桌面歌词".into(),
                    translation: "解锁后拖动 · 原生毛玻璃".into(),
                    start_ms: 0.0,
                    duration_ms: 3000.0,
                    ..Default::default()
                }),
                ..Default::default()
            };
            let interaction = Interaction::new(128, 420);
            let model = Rc::new(RefCell::new(Model {
                clock: PlaybackClock::new(&state),
                state,
                interaction,
                ready: true,
                received_state: true,
                position_initialized: true,
                map_revision: 0,
                input_rects: Vec::new(),
            }));
            let callback_model = model.clone();
            let callback_window = window.clone();
            let main_loop = glib::MainLoop::new(None, false);
            let chrome = Chrome::new(&area, move |action| {
                handle_action(&callback_model, &callback_window, &main_loop, action)
            });
            window.set_child(Some(&chrome.root));
            let draw_model = model.clone();
            let draw_root = chrome.root.clone();
            area.set_draw_func(move |_, cr, _, _| {
                let model = draw_model.borrow();
                let geometry = model.geometry(draw_root.width(), draw_root.height());
                cr.translate(-geometry.content.x, -geometry.content.y);
                render::draw(cr, &model.state, 1400.0, &geometry);
            });
            let drag = install_drag(&chrome.root, &chrome.glass, &area, &model, &window);
            window.present();
            settle();
            let geometry = model
                .borrow()
                .geometry(chrome.root.width(), chrome.root.height());
            chrome.update(&model.borrow().state, &geometry);
            settle();
            let mut picked = chrome
                .root
                .pick(
                    geometry.hotspot.x + 20.0,
                    geometry.hotspot.y + 20.0,
                    gtk::PickFlags::DEFAULT,
                )
                .expect("unlock hotspot must target a widget");
            while !picked.is::<gtk::Button>() {
                picked = picked.parent().expect("unlock must target native button");
            }
            let unlock = picked.downcast::<gtk::Button>().unwrap();
            snapshot(&window, "locked");
            unlock.emit_clicked();
            assert!(!model.borrow().state.locked);
            chrome.update(&model.borrow().state, &geometry);
            settle();
            snapshot(&window, "unlocked");
            let body = chrome
                .root
                .pick(
                    geometry.content.x + 80.0,
                    geometry.content.y + 100.0,
                    gtk::PickFlags::DEFAULT,
                )
                .unwrap();
            assert_eq!(
                body,
                chrome.glass.clone().upcast::<gtk::Widget>(),
                "transparent canvas must not intercept dragging"
            );
            // Exercise the same signal handlers used for pointer input. Move the
            // GTK panel between updates to ensure offsets use the stationary root.
            drag.emit_by_name::<()>(
                "drag-begin",
                &[&(geometry.content.x + 80.0), &(geometry.content.y + 100.0)],
            );
            drag.emit_by_name::<()>("drag-update", &[&40.0_f64, &(-30.0_f64)]);
            let moved = model
                .borrow()
                .geometry(chrome.root.width(), chrome.root.height());
            chrome.update(&model.borrow().state, &moved);
            settle();
            drag.emit_by_name::<()>("drag-update", &[&120.0_f64, &(-80.0_f64)]);
            drag.emit_by_name::<()>("drag-end", &[&120.0_f64, &(-80.0_f64)]);
            assert_eq!(
                model.borrow().state.placement.x,
                geometry.content.x as i32 + 120
            );
            assert_eq!(
                model.borrow().state.placement.y,
                geometry.content.y as i32 - 80
            );
            window.destroy();
            settle();
        });
    }
}
