#!/usr/bin/env python3
"""Replay actual wl_pointer press/release events on an isolated mock compositor.

Arguments: gtk4-layer-shell's test/mock-server/mock-server and the lyrics helper.
This never moves or clicks the user's desktop pointer.
"""
import json
import os
from pathlib import Path
import selectors
import subprocess
import sys
import tempfile
import time


def wait_for(predicate, message):
    deadline = time.monotonic() + 5
    while time.monotonic() < deadline:
        if predicate():
            return
        time.sleep(0.01)
    raise AssertionError(message)


def read_event(child):
    with selectors.DefaultSelector() as selector:
        selector.register(child.stdout, selectors.EVENT_READ)
        assert selector.select(5), "Timed out waiting for native event"
        line = child.stdout.readline()
        assert line, f"Helper exited before event (exit={child.poll()})"
        return json.loads(line)


def main():
    server_binary, helper_binary = map(lambda p: str(Path(p).resolve()), sys.argv[1:3])
    with tempfile.TemporaryDirectory(prefix="kugou-pointer-") as directory, \
            tempfile.TemporaryFile(mode="w+") as server_log, tempfile.TemporaryFile(mode="w+") as helper_log:
        environment = dict(os.environ, GTKLS_TEST_DIR=directory, XDG_RUNTIME_DIR=directory,
                           WAYLAND_DISPLAY=f"{directory}/gtkls-test-display", GDK_BACKEND="wayland",
                           GSK_RENDERER="cairo", GTK_A11Y="none", RUST_BACKTRACE="1")
        # A failed assertion aborts the Rust process; avoid leaving large cores.
        environment["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=/nonexistent-kugou-test-bus"
        response_path = f"{directory}/gtkls-test-response"
        os.mkfifo(response_path)
        response_fd = os.open(response_path, os.O_RDWR | os.O_NONBLOCK)
        server = subprocess.Popen([server_binary], env=environment, stdout=server_log, stderr=server_log)
        child = None
        command_fd = None
        try:
            wait_for(lambda: Path(environment["WAYLAND_DISPLAY"]).exists(), "Mock compositor did not start")
            wait_for(lambda: Path(f"{directory}/gtkls-test-command").exists(), "Mock command FIFO missing")
            command_fd = os.open(f"{directory}/gtkls-test-command", os.O_RDWR)
            child = subprocess.Popen([helper_binary], env=environment, stdin=subprocess.PIPE,
                                     stdout=subprocess.PIPE, stderr=helper_log, text=True)
            state = {"version": 1, "locked": True, "placement": {"has_value": True, "x": 10, "y": 10},
                     "current": {"text": "Pointer regression", "duration_ms": 1000}}
            child.stdin.write(json.dumps(state) + "\n")
            child.stdin.flush()
            assert read_event(child) == {"event": "ready", "version": 1}
            time.sleep(0.25)

            def click(x, y):
                os.write(command_fd, f"click_latest_surface {x} {y}\n".encode())
                with selectors.DefaultSelector() as selector:
                    selector.register(response_fd, selectors.EVENT_READ)
                    assert selector.select(5), "Mock pointer command timed out"
                    os.read(response_fd, 1024)

            # Mock compositor output is 1920x1080; a locked bubble is centered
            # in the 1024-wide lyric panel, with a top offset of 8.
            click(522, 38)
            assert read_event(child) == {"event": "action", "name": "unlock"}
            # Replay an old parent snapshot, then acknowledge the persisted
            # unlock. The helper must survive both and close normally at EOF.
            for locked in [True, False]:
                state["locked"] = locked
                child.stdin.write(json.dumps(state) + "\n")
                child.stdin.flush()
                time.sleep(0.15)
                assert child.poll() is None, "Helper died after unlock"
            # A toolbar button also denies the parent's drag sequence. Verify
            # the real lock and unlock click paths repeatedly, with parent acks.
            click(662, 44)
            assert read_event(child) == {"event": "action", "name": "lock"}
            state["locked"] = True
            child.stdin.write(json.dumps(state) + "\n")
            child.stdin.flush()
            time.sleep(0.25)
            click(522, 38)
            assert read_event(child) == {"event": "action", "name": "unlock"}
            state["locked"] = False
            child.stdin.write(json.dumps(state) + "\n")
            child.stdin.flush()
            time.sleep(0.15)
            child.stdin.close()
            assert child.wait(timeout=3) == 0
            print("PASS: real Wayland pointer lock/unlock, event delivery, stale snapshot, acknowledgement, clean exit")
        except BaseException:
            helper_log.seek(0)
            server_log.seek(0)
            print(helper_log.read()[-12000:], file=sys.stderr)
            print(server_log.read()[-5000:], file=sys.stderr)
            raise
        finally:
            for process in [child, server]:
                if process is not None and process.poll() is None:
                    process.kill()
                    process.wait()
            if command_fd is not None:
                os.close(command_fd)
            os.close(response_fd)


if __name__ == "__main__":
    main()
