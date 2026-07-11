#!/usr/bin/env bash
#
# Genera un paquete .deb instalable de CoreTempLinux.
#
# La app se publica en modo self-contained (incluye el runtime .NET 10),
# de modo que el .deb funciona en cualquier equipo amd64 sin instalar .NET.
#
# Uso:
#   ./build-deb.sh              # versión por defecto (1.0.0)
#   VERSION=1.2.3 ./build-deb.sh
#
# Resultado:  ./dist/coretemplinux_<version>_amd64.deb
#
set -euo pipefail

APPID="org.coretemplinux.App"
PKG="coretemplinux"
VERSION="${VERSION:-1.0.0}"
ARCH="amd64"
MAINTAINER="Oswaldo Sierra <oswaldox199@gmail.com>"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD="$SCRIPT_DIR/dist/${PKG}_${VERSION}"
DEB="$SCRIPT_DIR/dist/${PKG}_${VERSION}_${ARCH}.deb"

# Rutas dentro del paquete (destino en el sistema del usuario final).
LIBDIR="$BUILD/usr/lib/coretemplinux"
BINDIR="$BUILD/usr/bin"
ICONDIR="$BUILD/usr/share/icons/hicolor"
APPDIR="$BUILD/usr/share/applications"
DEBIANDIR="$BUILD/DEBIAN"

echo ">> Limpiando build anterior…"
rm -rf "$BUILD"
mkdir -p "$LIBDIR" "$BINDIR" "$ICONDIR" "$APPDIR" "$DEBIANDIR"

echo ">> Publicando (Release, self-contained)…"
dotnet publish "$SCRIPT_DIR" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o "$LIBDIR"

echo ">> Instalando iconos…"
for size in 16 32 48 64 128 256 512; do
    src="$SCRIPT_DIR/assets/icons/hicolor/${size}x${size}/apps/${APPID}.png"
    install -Dm644 "$src" "$ICONDIR/${size}x${size}/apps/${APPID}.png"
done

echo ">> Creando lanzador en /usr/bin…"
# Envoltorio que arranca el binario publicado.
cat > "$BINDIR/coretemplinux" <<'EOF'
#!/bin/sh
exec /usr/lib/coretemplinux/CoreTempLinux "$@"
EOF
chmod 755 "$BINDIR/coretemplinux"

echo ">> Instalando .desktop…"
sed "s|^Exec=.*|Exec=coretemplinux|" \
    "$SCRIPT_DIR/${APPID}.desktop" > "$APPDIR/${APPID}.desktop"
chmod 644 "$APPDIR/${APPID}.desktop"

# Tamaño instalado (KiB), para el campo Installed-Size.
INSTALLED_SIZE="$(du -sk "$BUILD/usr" | cut -f1)"

echo ">> Escribiendo metadatos DEBIAN/control…"
cat > "$DEBIANDIR/control" <<EOF
Package: $PKG
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Maintainer: $MAINTAINER
Installed-Size: $INSTALLED_SIZE
Depends: libc6, libgtk-4-1
Recommends: pipewire-bin | pulseaudio-utils, libnotify-bin
Description: Monitor de temperatura, frecuencia y carga de CPU
 Aplicación de escritorio GTK4 que muestra en tiempo real la temperatura
 de la CPU, la frecuencia y carga por núcleo y otros sensores hwmon, con
 alerta configurable (banner, notificación de escritorio y audio en bucle).
 Es la contraparte para Linux de la utilidad "Core Temp" de Windows.
EOF

# postinst / postrm: refrescan las cachés de iconos y de escritorio.
cat > "$DEBIANDIR/postinst" <<'EOF'
#!/bin/sh
set -e
gtk4-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null \
    || gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true
exit 0
EOF

cat > "$DEBIANDIR/postrm" <<'EOF'
#!/bin/sh
set -e
gtk4-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null \
    || gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true
exit 0
EOF

chmod 755 "$DEBIANDIR/postinst" "$DEBIANDIR/postrm"

echo ">> Construyendo el .deb…"
dpkg-deb --root-owner-group --build "$BUILD" "$DEB"

echo ""
echo "✔ Paquete generado:  $DEB"
echo "  Instalar con:      sudo apt install $DEB"
echo "  (o)                sudo dpkg -i $DEB"
