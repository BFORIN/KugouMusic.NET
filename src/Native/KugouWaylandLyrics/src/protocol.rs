use serde::{Deserialize, Serialize};
use std::time::Instant;

pub const VERSION: u32 = 1;
pub const MAX_MESSAGE_BYTES: usize = 1024 * 1024;

#[derive(Clone, Debug, Default, Deserialize)]
#[serde(default)]
pub struct Word {
    pub text: String,
    pub start_ms: f64,
    pub duration_ms: f64,
}

#[derive(Clone, Debug, Default, Deserialize)]
#[serde(default)]
pub struct Line {
    pub text: String,
    pub translation: String,
    pub start_ms: f64,
    pub duration_ms: f64,
    pub words: Vec<Word>,
}

impl Line {
    // Pango indexes text in UTF-8 bytes. Keep graphemes and shaped text intact;
    // interpolate the current word's visual clip rather than splitting strings.
    pub fn progress(&self, position_ms: f64) -> (usize, usize, f64) {
        if self.words.is_empty()
            || self
                .words
                .iter()
                .map(|w| w.text.as_str())
                .collect::<String>()
                != self.text
        {
            return (
                0,
                self.text.len(),
                fraction(position_ms, self.start_ms, self.duration_ms),
            );
        }
        let mut offset = 0;
        for word in &self.words {
            let end = offset + word.text.len();
            if position_ms < word.start_ms {
                return (offset, offset, 0.0);
            }
            if position_ms < word.start_ms + word.duration_ms.max(0.0) {
                return (
                    offset,
                    end,
                    fraction(position_ms, word.start_ms, word.duration_ms),
                );
            }
            offset = end;
        }
        (offset, offset, 1.0)
    }
}

fn fraction(position: f64, start: f64, duration: f64) -> f64 {
    if duration <= 0.0 {
        return if position >= start { 1.0 } else { 0.0 };
    }
    ((position - start) / duration).clamp(0.0, 1.0)
}

#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq)]
#[serde(default)]
pub struct Placement {
    pub has_value: bool,
    pub x: i32,
    pub y: i32,
    pub monitor: String,
}

#[derive(Clone, Debug, Deserialize)]
#[serde(default)]
pub struct State {
    pub version: u32,
    pub position_ms: f64,
    pub playing: bool,
    pub speed: f64,
    pub index: i32,
    pub current: Option<Line>,
    pub next: Option<Line>,
    pub font: String,
    pub font_size: f64,
    pub color: String,
    pub translation_color: String,
    pub translation: bool,
    pub double_line: bool,
    pub vertical: bool,
    pub alignment: String,
    pub locked: bool,
    pub controls_below: bool,
    pub over_fullscreen: bool,
    pub placement: Placement,
}

impl Default for State {
    fn default() -> Self {
        Self {
            version: VERSION,
            position_ms: 0.0,
            playing: false,
            speed: 1.0,
            index: -1,
            current: None,
            next: None,
            font: "sans-serif".into(),
            font_size: 30.0,
            color: "#FFFFFFFF".into(),
            translation_color: "#CCFFFFFF".into(),
            translation: true,
            double_line: false,
            vertical: false,
            alignment: "Center".into(),
            locked: false,
            controls_below: false,
            over_fullscreen: false,
            placement: Placement::default(),
        }
    }
}

impl State {
    pub fn validate(&mut self) -> Result<(), &'static str> {
        if self.version != VERSION {
            return Err("unsupported protocol version");
        }
        if !self.position_ms.is_finite() || !self.speed.is_finite() || !self.font_size.is_finite() {
            return Err("non-finite playback or font value");
        }
        for line in [&self.current, &self.next].into_iter().flatten() {
            if !line.start_ms.is_finite()
                || !line.duration_ms.is_finite()
                || line
                    .words
                    .iter()
                    .any(|w| !w.start_ms.is_finite() || !w.duration_ms.is_finite())
            {
                return Err("non-finite lyric timing");
            }
        }
        self.font_size = self.font_size.clamp(18.0, 50.0);
        self.speed = self.speed.clamp(0.0, 16.0);
        Ok(())
    }
}

pub struct PlaybackClock {
    position_ms: f64,
    speed: f64,
    received: Instant,
}

impl PlaybackClock {
    pub fn new(state: &State) -> Self {
        Self {
            position_ms: state.position_ms,
            speed: if state.playing { state.speed } else { 0.0 },
            received: Instant::now(),
        }
    }

    pub fn position_ms(&self) -> f64 {
        self.position_ms + self.received.elapsed().as_secs_f64() * 1000.0 * self.speed
    }
}

#[derive(Serialize)]
#[serde(tag = "event", rename_all = "snake_case")]
pub enum Event<'a> {
    Ready {
        version: u32,
    },
    Action {
        name: &'a str,
    },
    Placement {
        vertical: bool,
        placement: &'a Placement,
    },
}

pub fn emit(event: Event<'_>) {
    use std::io::Write;
    let mut out = std::io::stdout().lock();
    if let Ok(message) = serde_json::to_string(&event) {
        let _ = writeln!(out, "{message}");
        let _ = out.flush();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn word_progress_uses_utf8_offsets_and_waits_through_gaps() {
        let line = Line {
            text: "你好".into(),
            words: vec![
                Word {
                    text: "你".into(),
                    start_ms: 100.0,
                    duration_ms: 100.0,
                },
                Word {
                    text: "好".into(),
                    start_ms: 300.0,
                    duration_ms: 100.0,
                },
            ],
            ..Default::default()
        };
        assert_eq!(line.progress(50.0), (0, 0, 0.0));
        assert_eq!(line.progress(150.0), (0, 3, 0.5));
        assert_eq!(line.progress(250.0), (3, 3, 0.0));
        assert_eq!(line.progress(350.0), (3, 6, 0.5));
        assert_eq!(line.progress(450.0), (6, 6, 1.0));
    }

    #[test]
    fn mismatched_words_fall_back_to_line_timing() {
        let line = Line {
            text: "hello world".into(),
            start_ms: 100.0,
            duration_ms: 200.0,
            words: vec![Word {
                text: "hello".into(),
                ..Default::default()
            }],
            ..Default::default()
        };
        assert_eq!(line.progress(200.0), (0, 11, 0.5));
    }

    #[test]
    fn pause_freezes_clock_and_seek_replaces_anchor() {
        let state = State {
            position_ms: 1234.0,
            playing: false,
            ..Default::default()
        };
        assert_eq!(PlaybackClock::new(&state).position_ms(), 1234.0);
        let state = State {
            position_ms: 12.0,
            ..state
        };
        assert_eq!(PlaybackClock::new(&state).position_ms(), 12.0);
    }

    #[test]
    fn rejects_version_mismatch_and_invalid_numbers() {
        assert!(
            State {
                version: 2,
                ..Default::default()
            }
            .validate()
            .is_err()
        );
        assert!(
            State {
                speed: f64::NAN,
                ..Default::default()
            }
            .validate()
            .is_err()
        );
    }

    #[test]
    fn accepts_csharp_wire_format_and_escaped_text() {
        let mut state: State = serde_json::from_str(r##"{"version":1,"position_ms":123,"current":{"text":"你\t好\n🎵","words":[]},"placement":{"has_value":true,"x":24,"y":48,"monitor":"DP-1"}}"##).unwrap();
        state.validate().unwrap();
        assert_eq!(state.current.unwrap().text, "你\t好\n🎵");
        assert_eq!(state.placement.monitor, "DP-1");
    }
}
