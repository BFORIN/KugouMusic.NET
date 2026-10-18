fn main() {
    // The release package carries only the small layer-shell shim. GTK4 and its
    // graphics stack come from the distribution. Search beside the helper first.
    println!("cargo:rustc-link-arg=-Wl,-rpath,$ORIGIN/wayland");
    // Keep the layer-shell interposer ahead of libwayland-client in DT_NEEDED.
    println!("cargo:rustc-link-lib=gtk4-layer-shell");
}
