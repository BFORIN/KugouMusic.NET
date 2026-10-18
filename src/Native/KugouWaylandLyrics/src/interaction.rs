use crate::protocol::{Placement, State};
use std::time::{Duration, Instant};

// Playback snapshots can already be in flight when a native button is clicked.
// Keep local interactions until the parent acknowledges them, rather than
// letting an older snapshot immediately re-lock or move the surface back.
#[derive(Default)]
pub struct Interaction {
    pub x: i32,
    pub y: i32,
    pub drag_origin: Option<(i32, i32)>,
    pending_lock: Option<(bool, Instant)>,
    pending_placement: Option<(bool, Placement, Instant)>,
}

const ACK_TIMEOUT: Duration = Duration::from_secs(2);

impl Interaction {
    pub fn new(x: i32, y: i32) -> Self {
        Self {
            x,
            y,
            ..Default::default()
        }
    }

    pub fn set_locked(&mut self, state: &mut State, locked: bool) {
        state.locked = locked;
        self.pending_lock = Some((locked, Instant::now()));
    }

    pub fn reconcile(&mut self, state: &mut State) {
        if let Some((locked, sent)) = self.pending_lock {
            if state.locked == locked || sent.elapsed() > ACK_TIMEOUT {
                self.pending_lock = None;
            } else {
                state.locked = locked;
            }
        }
        if let Some((vertical, placement, sent)) = &self.pending_placement {
            if state.vertical != *vertical
                || state.placement == *placement
                || sent.elapsed() > ACK_TIMEOUT
            {
                self.pending_placement = None;
            } else {
                state.placement = placement.clone();
            }
        }
    }

    pub fn finish_drag(&mut self, state: &mut State, x: i32, y: i32, monitor: String) {
        self.x = x;
        self.y = y;
        self.drag_origin = None;
        state.placement = Placement {
            has_value: true,
            x,
            y,
            monitor,
        };
        self.pending_placement = Some((state.vertical, state.placement.clone(), Instant::now()));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn unlock_survives_an_older_playback_snapshot_until_acknowledged() {
        let mut interaction = Interaction::default();
        let mut state = State {
            locked: true,
            ..Default::default()
        };
        interaction.set_locked(&mut state, false);
        let mut stale = State {
            locked: true,
            ..state.clone()
        };
        interaction.reconcile(&mut stale);
        assert!(!stale.locked);
        interaction.reconcile(&mut state);
        assert!(interaction.pending_lock.is_none());
        let mut external_lock = State {
            locked: true,
            ..state
        };
        interaction.reconcile(&mut external_lock);
        assert!(external_lock.locked);
    }

    #[test]
    fn drag_position_survives_stale_parent_coordinates() {
        let mut interaction = Interaction::default();
        let mut state = State::default();
        let mut stale = state.clone();
        interaction.finish_drag(&mut state, 500, 240, "DP-1".into());
        interaction.reconcile(&mut stale);
        assert_eq!(stale.placement, state.placement);
        interaction.reconcile(&mut state);
        assert!(interaction.pending_placement.is_none());
    }

    #[test]
    fn switching_orientation_does_not_reuse_pending_position() {
        let mut interaction = Interaction::default();
        let mut state = State::default();
        interaction.finish_drag(&mut state, 500, 240, "DP-1".into());
        let mut vertical = State {
            vertical: true,
            ..Default::default()
        };
        interaction.reconcile(&mut vertical);
        assert!(!vertical.placement.has_value);
    }
}
