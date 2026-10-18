#!/usr/bin/env bash
set -euo pipefail

native_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
player_native="$native_dir/../../Apps/KugouAvaloniaPlayer/Native"
layer_license="${GTK4_LAYER_SHELL_LICENSE:-$native_dir/LICENSE.gtk4-layer-shell}"

if [[ ! -f "$layer_license" ]]; then
    printf 'Missing gtk4-layer-shell license: %s\nSet GTK4_LAYER_SHELL_LICENSE to the upstream LICENSE file.\n' "$layer_license" >&2
    exit 1
fi

pkg-config --atleast-version=4.6 gtk4
pkg-config --atleast-version=1.0 gtk4-layer-shell-0
cargo build --locked --release --target-dir "$native_dir/target" --manifest-path "$native_dir/Cargo.toml"

install -Dm755 "$native_dir/target/release/kugou-wayland-lyrics" "$player_native/kugou-wayland-lyrics"
layer_libdir="$(pkg-config --variable=libdir gtk4-layer-shell-0)"
install -Dm755 "$layer_libdir/libgtk4-layer-shell.so.0" "$player_native/wayland/libgtk4-layer-shell.so.0"
install -Dm644 "$layer_license" "$player_native/wayland/LICENSE.gtk4-layer-shell"
