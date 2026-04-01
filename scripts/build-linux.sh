#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION=${1:-Release}

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLUTION="$ROOT_DIR/VibeProxy.Linux.sln"
PROJECT="$ROOT_DIR/src/VibeProxy.Linux/VibeProxy.Linux.csproj"
OUT_DIR="$ROOT_DIR/out"
PUBLISH_DIR="$OUT_DIR/publish"
VERSION_LABEL="${APP_VERSION:-}"

if [ -z "$VERSION_LABEL" ] && [ -f "$ROOT_DIR/VERSION" ]; then
  VERSION_LABEL="$(tr -d '\r\n' < "$ROOT_DIR/VERSION")"
fi

if [ -z "$VERSION_LABEL" ]; then
  VERSION_LABEL="$CONFIGURATION"
fi

ZIP_PATH="$OUT_DIR/VibeProxy-Linux-${VERSION_LABEL}.zip"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "❌ dotnet SDK not found. Install dotnet-sdk (e.g., 'sudo pacman -S dotnet-sdk') and retry." >&2
  exit 1
fi

mkdir -p "$OUT_DIR"

echo "🔨 Restoring dependencies..."
dotnet restore "$SOLUTION"

echo "📦 Publishing self-contained linux-x64 binary..."
dotnet publish "$PROJECT" \
  -c "$CONFIGURATION" \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$PUBLISH_DIR"

if [ ! -d "$PUBLISH_DIR" ]; then
  echo "❌ Publish directory '$PUBLISH_DIR' was not created." >&2
  exit 1
fi

echo "🗜️ Packaging artifacts..."
rm -f "$ZIP_PATH"
cd "$PUBLISH_DIR"
zip -qr "$ZIP_PATH" .

echo "✅ Artifacts ready at $ZIP_PATH"
