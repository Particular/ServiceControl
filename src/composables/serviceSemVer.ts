const reSemver = /^v?((\d+)\.(\d+)\.(\d+))(?:-([\dA-Za-z\-_]+(?:\.[\dA-Za-z\-_]+)*))?(?:\+([\dA-Za-z\-_]+(?:\.[\dA-Za-z\-_]+)*))?$/;

export function isUpgradeAvailable(currentVersion: string, latestVersion: string) {
  const latest = parse(latestVersion.split("-")[0]);
  const current = parse(currentVersion.split("-")[0]);

  if (latest == null) return false;
  if (current == null) return false;

  if (latest.major !== current.major) {
    return latest.major > current.major;
  }
  if (latest.minor !== current.minor) {
    return latest.minor > current.minor;
  }
  if (latest.patch !== current.patch) {
    return latest.patch > current.patch;
  }

  return false;
}

export function isSupported(currentVersion: string, minSupportedVersion: string) {
  const minSupported = parse(minSupportedVersion);
  const current = parse(currentVersion);

  if (current == null) return false;
  if (minSupported == null) return true;

  if (minSupported.major !== current.major) {
    return minSupported.major <= current.major;
  }
  if (minSupported.minor !== current.minor) {
    return minSupported.minor <= current.minor;
  }
  if (minSupported.patch !== current.patch) {
    return minSupported.patch <= current.patch;
  }

  return true;
}

interface SemVer {
  semver: string | null;
  version: string;
  major: number;
  minor: number;
  patch: number;
  release: string;
  build: string;
}

function parse(version: string) {
  // semver, major, minor, patch
  // https://github.com/mojombo/semver/issues/32
  // https://github.com/isaacs/node-semver/issues/10
  // optional v
  const m = reSemver.exec(version) || [];

  function defaultToZero(num: string) {
    const n = parseInt(num, 10);

    return isNaN(n) ? 0 : n;
  }

  return 0 === m.length
    ? null
    : <SemVer>{
        semver: m[0],
        version: m[1],
        major: defaultToZero(m[2]),
        minor: defaultToZero(m[3]),
        patch: defaultToZero(m[4]),
        release: m[5],
        build: m[6],
      };
}
