#!/usr/bin/env bash
#
# Instala CoreTempLinux para el usuario actual:
#   - publica la app en ~/.local/lib/coretemplinux
#   - instala los iconos en el tema hicolor (~/.local/share/icons)
#   - instala el lanzador .desktop (~/.local/share/applications)
#
# Uso:   ./install.sh
# Prefijo alternativo (p. ej. para todo el sistema):
#        sudo PREFIX=/usr ./install.sh
#
set -euo pipefail

APPID="org.coretemplinux.App"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

PREFIX="${PREFIX:-$HOME/.local}"
LIBDIR="$PREFIX/lib/coretemplinux"
ICONDIR="$PREFIX/share/icons/hicolor"
APPDIR="$PREFIX/share/applications"

echo ">> Publicando (Release)…"
dotnet publish "$SCRIPT_DIR" -c Release -o "$LIBDIR"

echo ">> Instalando iconos en $ICONDIR"
for size in 16 32 48 64 128 256 512; do
    src="$SCRIPT_DIR/assets/icons/hicolor/${size}x${size}/apps/${APPID}.png"
    dst="$ICONDIR/${size}x${size}/apps"
    install -Dm644 "$src" "$dst/${APPID}.png"
done

echo ">> Instalando lanzador en $APPDIR"
mkdir -p "$APPDIR"
sed "s|^Exec=.*|Exec=$LIBDIR/CoreTempLinux|" \
    "$SCRIPT_DIR/${APPID}.desktop" > "$APPDIR/${APPID}.desktop"
chmod 644 "$APPDIR/${APPID}.desktop"

echo ">> Actualizando cachés…"
gtk4-update-icon-cache -q -t -f "$ICONDIR" 2>/dev/null \
    || gtk-update-icon-cache -q -t -f "$ICONDIR" 2>/dev/null || true
update-desktop-database "$APPDIR" 2>/dev/null || true

echo "✔ Instalado. Puede que necesites cerrar y reabrir sesión para ver el icono en el menú."
