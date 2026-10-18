use crate::protocol::{Line, State};
use gtk::{cairo, gdk, pango};
use unicode_segmentation::UnicodeSegmentation;

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Rect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

impl Rect {
    pub fn contains(self, x: f64, y: f64) -> bool {
        x >= self.x && y >= self.y && x < self.x + self.width && y < self.y + self.height
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Geometry {
    pub content: Rect,
    pub controls: Rect,
    pub hotspot: Rect,
}

pub fn geometry(state: &State, width: i32, height: i32, x: i32, y: i32) -> Geometry {
    let font = state.font_size;
    let content_width = if state.vertical {
        if state.double_line || state.translation {
            240.0
        } else {
            120.0
        }
    } else {
        1024.0_f64.min(width as f64)
    };
    let content_height = if state.vertical {
        620.0_f64.min(height as f64)
    } else {
        (font
            * if state.double_line {
                2.6
            } else if state.translation {
                2.2
            } else {
                1.4
            })
            + 88.0
    };
    let w = content_width.min((width as f64 - 24.0).max(1.0));
    let h = content_height.min((height as f64 - 24.0).max(1.0));
    let x = (x as f64).clamp(0.0, (width as f64 - w).max(0.0));
    let y = (y as f64).clamp(0.0, (height as f64 - h).max(0.0));
    let controls_width = (if state.vertical { 212.0_f64 } else { 412.0_f64 }).min(width as f64);
    let controls_height = if state.vertical { 92.0 } else { 52.0 };
    let controls = Rect {
        x: (x + (w - controls_width) / 2.0).clamp(0.0, (width as f64 - controls_width).max(0.0)),
        y: if state.controls_below {
            y + h - controls_height - 8.0
        } else {
            y + 8.0
        },
        width: controls_width,
        height: controls_height,
    };
    Geometry {
        content: Rect {
            x,
            y,
            width: w,
            height: h,
        },
        hotspot: Rect {
            x: x + (w - 40.0) / 2.0,
            y: if state.controls_below {
                y + h - 48.0
            } else {
                y + 8.0
            },
            width: 40.0,
            height: 40.0,
        },
        controls,
    }
}

pub fn draw(cr: &cairo::Context, state: &State, position_ms: f64, geometry: &Geometry) {
    let bounds = geometry.content;
    let top = bounds.y
        + if state.controls_below {
            12.0
        } else {
            geometry.controls.height + 20.0
        };
    let available = Rect {
        x: bounds.x + 12.0,
        y: top,
        width: (bounds.width - 24.0).max(1.0),
        height: (bounds.height - geometry.controls.height - 32.0).max(1.0),
    };
    if state.vertical {
        vertical(cr, state, position_ms, available);
    } else {
        let current = state.current.as_ref();
        let next = if state.double_line {
            state.next.as_ref()
        } else {
            None
        };
        let (first, second) = if state.double_line && state.index % 2 != 0 {
            (next, current)
        } else {
            (current, next)
        };
        for (i, line) in [first, second].into_iter().enumerate() {
            if let Some(line) = line {
                let active = std::ptr::eq(line, current.unwrap_or(line)) && current.is_some();
                draw_line(
                    cr,
                    state,
                    line,
                    active.then_some(position_ms),
                    available.x,
                    available.y + i as f64 * state.font_size * 1.3,
                    available.width,
                );
            }
        }
        if !state.double_line
            && state.translation
            && let Some(line) = current
        {
            text(
                cr,
                &line.translation,
                &state.font,
                state.font_size * 0.6,
                &state.translation_color,
                Rect {
                    y: available.y + state.font_size * 1.35,
                    ..available
                },
                &state.alignment,
            );
        }
    }
}

fn layout(cr: &cairo::Context, value: &str, font: &str, size: f64) -> pango::Layout {
    let layout = pangocairo::functions::create_layout(cr);
    let mut description = pango::FontDescription::from_string(font);
    description.set_absolute_size(size * pango::SCALE as f64);
    layout.set_font_description(Some(&description));
    layout.set_text(value);
    layout
}

fn rgba(value: &str) -> gdk::RGBA {
    // C# sends #AARRGGBB; GTK expects #RRGGBBAA.
    let css = if value.len() == 9 && value.is_ascii() && value.starts_with('#') {
        format!("#{}{}", &value[3..], &value[1..3])
    } else {
        value.to_owned()
    };
    gdk::RGBA::parse(css).unwrap_or(gdk::RGBA::WHITE)
}

fn paint(cr: &cairo::Context, layout: &pango::Layout, color: &str, opacity: f64) {
    let color = rgba(color);
    pangocairo::functions::layout_path(cr, layout);
    cr.set_source_rgba(0.0, 0.0, 0.0, 0.85 * opacity);
    cr.set_line_width(2.5);
    cr.set_line_join(cairo::LineJoin::Round);
    let _ = cr.stroke_preserve();
    cr.set_source_rgba(
        color.red() as f64,
        color.green() as f64,
        color.blue() as f64,
        color.alpha() as f64 * opacity,
    );
    let _ = cr.fill();
}

fn aligned_x(alignment: &str, x: f64, available: f64, width: f64) -> f64 {
    x + match alignment {
        "Left" => 0.0,
        "Right" => available - width,
        _ => (available - width) / 2.0,
    }
}

fn text(
    cr: &cairo::Context,
    value: &str,
    font: &str,
    size: f64,
    color: &str,
    rect: Rect,
    alignment: &str,
) {
    let layout = layout(cr, value, font, size);
    let (width, _) = layout.pixel_size();
    let scale = (rect.width / (width.max(1) as f64)).min(1.0);
    let _ = cr.save();
    cr.translate(
        aligned_x(alignment, rect.x, rect.width, width as f64 * scale),
        rect.y,
    );
    cr.scale(scale, scale);
    cr.move_to(0.0, 0.0);
    paint(cr, &layout, color, 1.0);
    let _ = cr.restore();
}

fn draw_line(
    cr: &cairo::Context,
    state: &State,
    line: &Line,
    position: Option<f64>,
    x: f64,
    y: f64,
    available: f64,
) {
    let layout = layout(cr, &line.text, &state.font, state.font_size);
    let (width, height) = layout.pixel_size();
    let scale = (available / width.max(1) as f64).min(1.0);
    let _ = cr.save();
    cr.translate(
        aligned_x(&state.alignment, x, available, width as f64 * scale),
        y,
    );
    cr.scale(scale, scale);
    cr.move_to(0.0, 0.0);
    paint(cr, &layout, &state.color, 0.4);
    if let Some(position) = position {
        let (start, end, fraction) = line.progress(position);
        let start_x = layout.index_to_pos(start as i32).x() as f64 / pango::SCALE as f64;
        let end_x = layout.index_to_pos(end as i32).x() as f64 / pango::SCALE as f64;
        let edge = start_x + (end_x - start_x) * fraction;
        cr.rectangle(-2.0, -2.0, edge + 2.0, height as f64 + 4.0);
        cr.clip();
        cr.move_to(0.0, 0.0);
        paint(cr, &layout, &state.color, 1.0);
    }
    let _ = cr.restore();
}

fn vertical(cr: &cairo::Context, state: &State, position: f64, rect: Rect) {
    let mut x = rect.x + rect.width - state.font_size;
    for (line, active, size, color) in [
        (
            state.current.as_ref(),
            true,
            state.font_size,
            state.color.as_str(),
        ),
        (
            if state.double_line {
                state.next.as_ref()
            } else {
                None
            },
            false,
            state.font_size,
            state.color.as_str(),
        ),
    ] {
        let Some(line) = line else {
            continue;
        };
        let rows = (rect.height / (size * 1.16)).floor().max(1.0) as usize;
        let (start, end, fraction) = line.progress(position);
        for (i, (byte, grapheme)) in line.text.grapheme_indices(true).enumerate() {
            let column_x = x - (i / rows) as f64 * (size + 8.0);
            if column_x < rect.x {
                break;
            }
            let y = rect.y + (i % rows) as f64 * size * 1.16;
            let layout = layout(cr, grapheme, &state.font, size);
            cr.move_to(column_x, y);
            paint(cr, &layout, color, 0.4);
            let progress = if !active {
                0.0
            } else if byte < start {
                1.0
            } else if byte < end {
                let total = line.text[start..end].graphemes(true).count().max(1);
                let before = line.text[start..byte].graphemes(true).count();
                (fraction * total as f64 - before as f64).clamp(0.0, 1.0)
            } else {
                0.0
            };
            if progress > 0.0 {
                let _ = cr.save();
                cr.rectangle(
                    column_x - 2.0,
                    y - 2.0,
                    size + 4.0,
                    (size * 1.16 + 4.0) * progress,
                );
                cr.clip();
                cr.move_to(column_x, y);
                paint(cr, &layout, color, 1.0);
                let _ = cr.restore();
            }
        }
        let columns = line.text.graphemes(true).count().div_ceil(rows).max(1);
        x -= columns as f64 * (size + 8.0) + 12.0;
        if active && state.translation && !state.double_line {
            let small = size * 0.6;
            let rows = (rect.height / (small * 1.16)).floor().max(1.0) as usize;
            for (i, grapheme) in line.translation.graphemes(true).enumerate() {
                let column_x = x - (i / rows) as f64 * (small + 6.0);
                if column_x < rect.x {
                    break;
                }
                cr.move_to(column_x, rect.y + (i % rows) as f64 * small * 1.16);
                paint(
                    cr,
                    &layout(cr, grapheme, &state.font, small),
                    &state.translation_color,
                    1.0,
                );
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn placement_is_clamped_to_output_and_hotspot_remains_reachable() {
        let state = State::default();
        let g = geometry(&state, 800, 600, 4000, -100);
        assert_eq!(g.content.x, 24.0);
        assert_eq!(g.content.y, 0.0);
        assert!(g.hotspot.x >= 0.0 && g.hotspot.x + g.hotspot.width <= 800.0);
    }

    #[test]
    fn renders_both_layouts_and_leaves_outside_pixels_transparent() {
        use std::io::Write;
        for vertical in [false, true] {
            let state = State {
                vertical,
                color: "#FF5BCBFF".into(),
                current: Some(Line {
                    text: "你好，Wayland 桌面歌词 🎵".into(),
                    translation: "Native lyrics · 鼠标穿透".into(),
                    start_ms: 1000.0,
                    duration_ms: 3000.0,
                    ..Default::default()
                }),
                ..Default::default()
            };
            let mut surface =
                cairo::ImageSurface::create(cairo::Format::ARgb32, 1280, 720).unwrap();
            let cr = cairo::Context::new(&surface).unwrap();
            let geometry = geometry(
                &state,
                1280,
                720,
                if vertical { 980 } else { 128 },
                if vertical { 40 } else { 500 },
            );
            draw(&cr, &state, 2500.0, &geometry);
            drop(cr);
            surface.flush();
            let stride = surface.stride() as usize;
            let data = surface.data().unwrap();
            assert_eq!(&data[..4], &[0, 0, 0, 0]);
            assert!(data.as_chunks::<4>().0.iter().filter(|p| p[3] > 0).count() > 1000);
            assert!(
                data.as_chunks::<4>()
                    .0
                    .iter()
                    .filter(|p| p[0] > 100 && p[1] > 80)
                    .count()
                    > 100
            );
            if let Ok(directory) = std::env::var("KUGOU_WAYLAND_PREVIEW_DIR") {
                std::fs::create_dir_all(&directory).unwrap();
                let path = std::path::Path::new(&directory).join(if vertical {
                    "vertical.ppm"
                } else {
                    "horizontal.ppm"
                });
                let mut file = std::fs::File::create(path).unwrap();
                write!(file, "P6\n1280 720\n255\n").unwrap();
                for y in 0..720 {
                    for x in 0..1280 {
                        let pixel = &data[y * stride + x * 4..][..4];
                        let argb = u32::from_ne_bytes(pixel.try_into().unwrap());
                        file.write_all(&[(argb >> 16) as u8, (argb >> 8) as u8, argb as u8])
                            .unwrap();
                    }
                }
            }
        }
    }
}
