#!/usr/bin/env python3
"""Run against a layer-shell desktop; briefly displays a test lyric surface."""
import json
import os
import re
from pathlib import Path
import selectors
import subprocess
import sys
import tempfile
import time


def main():
    binary = Path(sys.argv[1]).resolve()
    environment = dict(os.environ, WAYLAND_DEBUG="client", GSK_RENDERER="cairo")
    state = {
        "version": 1,
        "position_ms": 1500,
        "playing": False,
        "current": {
            "text": "你好，Wayland 🎵",
            "translation": "Native desktop lyrics",
            "start_ms": 1000,
            "duration_ms": 3000,
            "words": [
                {"text": "你好，", "start_ms": 1000, "duration_ms": 1000},
                {"text": "Wayland 🎵", "start_ms": 2000, "duration_ms": 2000},
            ],
        },
        "locked": True,
    }
    with tempfile.TemporaryFile(mode="w+") as trace:
        child = subprocess.Popen([str(binary)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                 stderr=trace, text=True, env=environment)
        try:
            child.stdin.write(json.dumps(state) + "\n")
            child.stdin.flush()
            with selectors.DefaultSelector() as selector:
                selector.register(child.stdout, selectors.EVENT_READ)
                assert selector.select(5), "No ready handshake within 5 seconds"
                ready = json.loads(child.stdout.readline())
            assert ready == {"event": "ready", "version": 1}, ready
            time.sleep(0.2)
            for update in [
                {"locked": False},
                {"vertical": True, "double_line": True},
                {"over_fullscreen": True, "locked": True},
                {"over_fullscreen": False, "vertical": False},
            ]:
                state.update(update)
                child.stdin.write(json.dumps(state) + "\n")
                child.stdin.flush()
                time.sleep(0.2)
            child.stdin.close()
            assert child.wait(timeout=3) == 0
            trace.seek(0)
            messages = trace.read()
            assert "get_layer_surface" in messages, "No layer-shell surface created"
            assert "set_input_region" in messages, "No input region submitted"
            regions = [(int(w), int(h)) for w, h in re.findall(
                r"wl_region[#@]\d+\.add\(-?\d+, -?\d+, (\d+), (\d+)\)", messages)]
            assert (40, 40) in regions, f"Locked input region is not the unlock hotspot: {regions}"
            assert any(w >= 200 and h >= 100 for w, h in regions), "Unlocked lyric input region missing"
            assert messages.count(".set_input_region(") >= 4, "Paused state updates did not commit input regions"
            assert messages.count(".attach(wl_buffer") >= 4, "Paused state updates did not repaint lyrics"
            assert "set_layer(3)" in messages, "Overlay layer transition missing"
            assert "set_layer(2)" in messages, "Top layer transition missing"
            assert "protocol error" not in messages.lower(), messages[-5000:]
            assert "CRITICAL" not in messages, messages[-5000:]
            assert "Theme parsing error" not in messages, messages[-5000:]
            if "backdrop-filter available" in messages and "ext_background_effect_manager_v1" in messages:
                assert ".set_blur_region(wl_region" in messages, "GTK did not submit a compositor blur region"
                blur_regions = re.findall(r"ext_background_effect_surface_v1[#@]\d+\.set_blur_region\(([^)]*)\)", messages)
                assert blur_regions[-1] == "nil", f"Locked state left a compositor blur region: {blur_regions}"
            print("PASS: native niri/layer-shell handshake, input regions, layout, layer changes, stdin EOF")
        except BaseException:
            trace.seek(0)
            print(trace.read()[-12000:], file=sys.stderr)
            raise
        finally:
            if child.poll() is None:
                child.kill()
                child.wait()


if __name__ == "__main__":
    main()
