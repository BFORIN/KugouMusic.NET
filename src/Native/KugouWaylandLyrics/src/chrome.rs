use crate::{protocol::State, render::Geometry};
use gtk::{gdk, prelude::*};
use std::{
    cell::{Cell, RefCell},
    rc::Rc,
};

pub const ACTIONS: [(&str, &str, &str); 10] = [
    ("previous", "media-skip-backward-symbolic", "上一首"),
    ("play_pause", "media-playback-start-symbolic", "播放 / 暂停"),
    ("next", "media-skip-forward-symbolic", "下一首"),
    ("smaller", "", "缩小字号"),
    ("larger", "", "增大字号"),
    ("translation", "", "显示 / 隐藏翻译"),
    (
        "placement",
        "view-sort-descending-symbolic",
        "操作栏置上 / 置下",
    ),
    ("monitor", "video-display-symbolic", "切换显示器"),
    ("lock", "changes-prevent-symbolic", "锁定并启用鼠标穿透"),
    ("close", "window-close-symbolic", "关闭桌面歌词"),
];

pub struct Chrome {
    pub root: gtk::Overlay,
    stage: gtk::Fixed,
    pub glass: gtk::Box,
    area: gtk::DrawingArea,
    toolbar: gtk::Revealer,
    grid: gtk::Grid,
    buttons: Vec<gtk::Button>,
    unlock: gtk::Revealer,
    last_layout: Cell<Option<bool>>,
    last_geometry: RefCell<Option<Geometry>>,
    last_locked: Cell<Option<bool>>,
    last_playing: Cell<Option<bool>>,
}

impl Chrome {
    pub fn new(area: &gtk::DrawingArea, on_action: impl Fn(&str) + 'static) -> Self {
        let root = gtk::Overlay::new();
        root.set_child(Some(&gtk::Box::new(gtk::Orientation::Horizontal, 0)));
        let stage = gtk::Fixed::builder().hexpand(true).vexpand(true).build();
        root.add_overlay(&stage);
        root.set_measure_overlay(&stage, false);
        let glass = gtk::Box::new(gtk::Orientation::Horizontal, 0);
        glass.add_css_class("lyrics-glass");
        glass.add_css_class("locked");
        glass.set_cursor_from_name(Some("grab"));
        glass.set_tooltip_text(Some("拖动调整歌词位置"));
        stage.put(&glass, 0.0, 0.0);
        area.set_can_target(false);
        stage.put(area, 0.0, 0.0);

        let on_action = Rc::new(on_action);
        let grid = gtk::Grid::builder()
            .column_spacing(4)
            .row_spacing(4)
            .column_homogeneous(true)
            .build();
        grid.add_css_class("lyrics-toolbar");
        let mut buttons = Vec::new();
        for (index, (action, icon, title)) in ACTIONS.iter().enumerate() {
            let button = gtk::Button::new();
            button.set_focusable(false);
            button.set_cursor_from_name(Some("pointer"));
            button.set_tooltip_text(Some(title));
            if icon.is_empty() {
                button.set_label(match *action {
                    "smaller" => "A−",
                    "larger" => "A+",
                    _ => "译",
                });
            } else {
                let image = gtk::Image::from_icon_name(icon);
                image.set_pixel_size(18);
                button.set_child(Some(&image));
            }
            if *action == "play_pause" {
                button.add_css_class("play");
            }
            if *action == "close" {
                button.add_css_class("close");
            }
            let callback = on_action.clone();
            button.connect_clicked(move |_| callback(ACTIONS[index].0));
            grid.attach(&button, index as i32, 0, 1, 1);
            buttons.push(button);
        }
        let toolbar = gtk::Revealer::builder()
            .transition_type(gtk::RevealerTransitionType::Crossfade)
            .transition_duration(180)
            .child(&grid)
            .build();
        stage.put(&toolbar, 0.0, 0.0);

        let unlock_button = gtk::Button::new();
        unlock_button.add_css_class("lyrics-unlock");
        unlock_button.set_focusable(false);
        unlock_button.set_cursor_from_name(Some("pointer"));
        unlock_button.set_tooltip_text(Some("解锁后可拖动歌词"));
        let image = gtk::Image::from_icon_name("changes-prevent-symbolic");
        image.set_pixel_size(18);
        unlock_button.set_child(Some(&image));
        unlock_button.connect_clicked(move |_| on_action("unlock"));
        let unlock = gtk::Revealer::builder()
            .transition_type(gtk::RevealerTransitionType::Crossfade)
            .transition_duration(180)
            .child(&unlock_button)
            .build();
        stage.put(&unlock, 0.0, 0.0);

        Self {
            root,
            stage,
            glass,
            area: area.clone(),
            toolbar,
            grid,
            buttons,
            unlock,
            last_layout: Cell::new(None),
            last_geometry: RefCell::new(None),
            last_locked: Cell::new(None),
            last_playing: Cell::new(None),
        }
    }

    pub fn update(&self, state: &State, geometry: &Geometry) {
        if self.last_layout.replace(Some(state.vertical)) != Some(state.vertical) {
            for button in &self.buttons {
                self.grid.remove(button);
            }
            for (index, button) in self.buttons.iter().enumerate() {
                let columns = if state.vertical { 5 } else { 10 };
                self.grid.attach(
                    button,
                    (index % columns) as i32,
                    (index / columns) as i32,
                    1,
                    1,
                );
            }
        }
        if self.last_locked.replace(Some(state.locked)) != Some(state.locked) {
            if state.locked {
                self.glass.add_css_class("locked");
                self.grid.add_css_class("locked");
            } else {
                self.glass.remove_css_class("locked");
                self.grid.remove_css_class("locked");
            }
            self.glass.set_can_target(!state.locked);
            self.toolbar.set_reveal_child(!state.locked);
            self.toolbar.set_can_target(!state.locked);
            self.unlock.set_reveal_child(state.locked);
            self.unlock.set_can_target(state.locked);
        }
        if self.last_playing.replace(Some(state.playing)) != Some(state.playing) {
            let image = gtk::Image::from_icon_name(if state.playing {
                "media-playback-pause-symbolic"
            } else {
                "media-playback-start-symbolic"
            });
            image.set_pixel_size(18);
            self.buttons[1].set_child(Some(&image));
        }
        if self.last_geometry.borrow().as_ref() != Some(geometry) {
            for (widget, rect) in [
                (self.glass.upcast_ref::<gtk::Widget>(), geometry.content),
                (self.area.upcast_ref(), geometry.content),
                (self.toolbar.upcast_ref(), geometry.controls),
                (self.unlock.upcast_ref(), geometry.hotspot),
            ] {
                self.stage.move_(widget, rect.x, rect.y);
                widget.set_size_request(rect.width as i32, rect.height as i32);
            }
            *self.last_geometry.borrow_mut() = Some(*geometry);
        }
    }
}

pub fn install_css(display: &gdk::Display) {
    let css = gtk::CssProvider::new();
    css.load_from_data(include_str!("chrome.css"));
    gtk::style_context_add_provider_for_display(
        display,
        &css,
        gtk::STYLE_PROVIDER_PRIORITY_APPLICATION,
    );

    // New GTK releases turn backdrop-filter into ext-background-effect blur
    // requests, including the widget's rounded shape. Probe parsing at runtime
    // so older GTK installations retain the translucent tint and border.
    let blur = gtk::CssProvider::new();
    let unsupported = Rc::new(Cell::new(false));
    let parsing_failed = unsupported.clone();
    blur.connect_parsing_error(move |_, _, _| parsing_failed.set(true));
    blur.load_from_data(
        ".lyrics-glass:not(.locked), .lyrics-toolbar:not(.locked) { backdrop-filter: blur(18px); }
         .lyrics-glass.locked, .lyrics-toolbar.locked { backdrop-filter: none; }",
    );
    if !unsupported.get() {
        gtk::style_context_add_provider_for_display(
            display,
            &blur,
            gtk::STYLE_PROVIDER_PRIORITY_APPLICATION + 1,
        );
        eprintln!("Wayland lyrics: backdrop-filter available");
    } else {
        eprintln!(
            "Wayland lyrics: this GTK version does not support backdrop-filter; using translucent glass"
        );
    }
}
