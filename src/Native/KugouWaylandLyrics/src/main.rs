mod chrome;
mod interaction;
mod protocol;
mod render;
mod window;

fn main() {
    // The helper must never silently start an X11 GTK window.
    unsafe {
        std::env::set_var("GDK_BACKEND", "wayland");
    }
    if let Err(error) = window::run() {
        eprintln!("Wayland lyrics: {error}");
        std::process::exit(1);
    }
}
