use regex::Regex;
use reqwest::blocking::Client;
use semver::Version;
use serde::Deserialize;

use super::paths::{GITHUB_RELEASES_URL, MOD_ZIP_PREFIX, MOD_ZIP_SUFFIX};

#[derive(Debug, Clone, Deserialize)]
pub struct ReleaseInfo {
    pub tag_name: String,
    #[serde(default)]
    pub prerelease: bool,
    /// The release notes: the version's CHANGELOG.md section, per create-release.ps1.
    #[serde(default)]
    pub body: Option<String>,
    #[serde(default)]
    pub assets: Vec<Asset>,
}

impl ReleaseInfo {
    pub fn version(&self) -> Option<Version> {
        Version::parse(self.tag_name.strip_prefix('v')?).ok()
    }
}

#[derive(Debug, Clone, Deserialize)]
pub struct Asset {
    pub name: String,
    pub browser_download_url: String,
    #[serde(default)]
    pub digest: Option<String>,
}

impl Asset {
    pub fn sha256_digest(&self) -> Option<String> {
        let digest = self.digest.as_deref()?.trim();
        let (algo, hex) = digest.split_once(':')?;
        if !algo.eq_ignore_ascii_case("sha256") {
            return None;
        }
        Some(hex.trim().to_lowercase())
    }

    pub fn version(&self) -> Option<String> {
        parse_mod_zip_version(&self.name)
    }
}

pub fn fetch_releases() -> Result<Vec<ReleaseInfo>, String> {
    let client = Client::builder()
        .user_agent("NonVisualCalculusInstaller")
        .timeout(std::time::Duration::from_secs(30))
        .build()
        .map_err(|e| format!("Failed to create HTTP client: {e}"))?;

    let response = client
        .get(GITHUB_RELEASES_URL)
        .send()
        .map_err(|e| format!("Failed to connect to GitHub: {e}"))?;

    if !response.status().is_success() {
        return Err(format!("GitHub API returned {}", response.status()));
    }

    response
        .json::<Vec<ReleaseInfo>>()
        .map_err(|e| format!("Failed to parse GitHub releases: {e}"))
}

/// The newest installable release: highest version, not a prerelease, carrying a mod zip.
pub fn latest_release(releases: &[ReleaseInfo]) -> Option<&ReleaseInfo> {
    releases
        .iter()
        .filter(|r| !r.prerelease && find_mod_zip(r).is_some())
        .filter(|r| r.version().is_some())
        .max_by_key(|r| r.version().unwrap())
}

/// Release notes for every version after `from` up to and including `to`, oldest first.
pub fn changelog_between<'a>(
    releases: &'a [ReleaseInfo],
    from: &Version,
    to: &Version,
) -> Vec<(Version, &'a str)> {
    let mut entries: Vec<(Version, &'a str)> = releases
        .iter()
        .filter(|r| !r.prerelease)
        .filter_map(|r| {
            let version = r.version()?;
            if version <= *from || version > *to {
                return None;
            }
            let body = r.body.as_deref()?.trim();
            if body.is_empty() {
                return None;
            }
            Some((version, body))
        })
        .collect();
    entries.sort_by(|a, b| a.0.cmp(&b.0));
    entries
}

pub fn find_mod_zip(release: &ReleaseInfo) -> Option<Asset> {
    release
        .assets
        .iter()
        .find(|asset| parse_mod_zip_version(&asset.name).is_some())
        .cloned()
}

pub fn parse_mod_zip_version(name: &str) -> Option<String> {
    let escaped_prefix = regex::escape(MOD_ZIP_PREFIX);
    let escaped_suffix = regex::escape(MOD_ZIP_SUFFIX);
    let pattern = format!(
        r"^{}(?P<version>\d+\.\d+\.\d+){}$",
        escaped_prefix, escaped_suffix
    );
    let re = Regex::new(&pattern).unwrap();
    re.captures(name)
        .and_then(|caps| caps.name("version").map(|m| m.as_str().to_string()))
}

pub fn download_asset(asset: &Asset, dest: &std::path::Path) -> Result<(), String> {
    let client = Client::builder()
        .user_agent("NonVisualCalculusInstaller")
        .timeout(std::time::Duration::from_secs(120))
        .build()
        .map_err(|e| format!("Failed to create HTTP client: {e}"))?;

    let mut response = client
        .get(&asset.browser_download_url)
        .send()
        .map_err(|e| format!("Download failed: {e}"))?;

    if !response.status().is_success() {
        return Err(format!("Download returned {}", response.status()));
    }

    if let Some(parent) = dest.parent() {
        std::fs::create_dir_all(parent).map_err(|e| format!("Failed to create temp dir: {e}"))?;
    }
    let mut file =
        std::fs::File::create(dest).map_err(|e| format!("Failed to create downloaded zip: {e}"))?;
    std::io::copy(&mut response, &mut file).map_err(|e| format!("Failed to save zip: {e}"))?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn release(tag: &str, prerelease: bool, body: Option<&str>, with_zip: bool) -> ReleaseInfo {
        let assets = if with_zip {
            let version = tag.strip_prefix('v').unwrap_or(tag);
            vec![Asset {
                name: format!("{MOD_ZIP_PREFIX}{version}{MOD_ZIP_SUFFIX}"),
                browser_download_url: "https://example.invalid/mod.zip".to_string(),
                digest: None,
            }]
        } else {
            Vec::new()
        };
        ReleaseInfo {
            tag_name: tag.to_string(),
            prerelease,
            body: body.map(str::to_string),
            assets,
        }
    }

    #[test]
    fn parses_mod_zip_version() {
        assert_eq!(
            parse_mod_zip_version("NonVisualCalculus-v1.0.0.zip").as_deref(),
            Some("1.0.0")
        );
        assert!(parse_mod_zip_version("source.zip").is_none());
        assert!(parse_mod_zip_version("NonVisualCalculusInstaller.exe").is_none());
    }

    #[test]
    fn extracts_sha256_digest() {
        let asset = Asset {
            name: "x.zip".to_string(),
            browser_download_url: "https://example.invalid/x.zip".to_string(),
            digest: Some("sha256:ABCDEF".to_string()),
        };
        assert_eq!(asset.sha256_digest().as_deref(), Some("abcdef"));
    }

    #[test]
    fn parses_release_version_from_tag() {
        assert_eq!(
            release("v1.2.3", false, None, false).version(),
            Some(Version::new(1, 2, 3))
        );
        assert_eq!(release("nightly", false, None, false).version(), None);
    }

    #[test]
    fn latest_release_picks_highest_installable() {
        let releases = vec![
            release("v1.0.0", false, None, true),
            release("v2.0.0-rc", true, None, true),
            release("v1.2.0", false, None, true),
            release("v9.9.9", false, None, false),
        ];
        assert_eq!(
            latest_release(&releases).map(|r| r.tag_name.as_str()),
            Some("v1.2.0")
        );
        assert!(latest_release(&[]).is_none());
    }

    #[test]
    fn changelog_between_selects_half_open_range_oldest_first() {
        let releases = vec![
            release("v1.2.0", false, Some("newest"), true),
            release("v1.1.0", false, Some("middle"), true),
            release("v1.0.1", false, Some("oldest"), true),
            release("v1.0.0", false, Some("start, excluded"), true),
            release("v1.3.0", false, Some("beyond target, excluded"), true),
            release("v1.1.5", true, Some("prerelease, excluded"), true),
            release("v1.1.6", false, Some("   "), true),
        ];
        let from = Version::new(1, 0, 0);
        let to = Version::new(1, 2, 0);
        let entries = changelog_between(&releases, &from, &to);
        let bodies: Vec<&str> = entries.iter().map(|(_, body)| *body).collect();
        assert_eq!(bodies, vec!["oldest", "middle", "newest"]);
        assert_eq!(entries[0].0, Version::new(1, 0, 1));
    }

    #[test]
    fn changelog_between_is_empty_for_reinstall() {
        let releases = vec![release("v1.0.0", false, Some("notes"), true)];
        let same = Version::new(1, 0, 0);
        assert!(changelog_between(&releases, &same, &same).is_empty());
    }
}
