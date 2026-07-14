fn main() {
    if std::env::var("CARGO_CFG_TARGET_OS").unwrap_or_default() == "windows" {
        // compile_for, not compile: the manifest requires elevation, which must
        // not apply to the test harness binary or `cargo test` cannot run.
        let _ = embed_resource::compile_for(
            "app.rc",
            ["whirling-in-words-installer"],
            embed_resource::NONE,
        );
    }
}
