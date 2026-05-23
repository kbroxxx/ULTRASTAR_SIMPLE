# UltraStar Simplificado

Aplicación de karaoke simplificada para Windows, basada en el formato UltraStar.  
Reproducción de videos con letras sincronizadas usando **mpv** como motor.

## Características

- Biblioteca de canciones con caratulas y búsqueda en tiempo real
- Vista previa de canciones (audio + video) al navegar
- Reproductor karaoke con letras animadas (progreso por frase)
- Pelota de compás que indica cuándo entrar a cantar
- Control de tono (pitch) y nivel vocal/karaoke
- Sistema de favoritos persistente
- Compatible con miles de canciones sin lag (renderizado virtual)
- Soporte para fondo personalizado (`fondo.jpg`)

## Requisitos

- Windows 10/11
- [mpv player](https://mpv.io/installation/) — colocar `mpv.exe` en la carpeta del programa
- [ffmpeg](https://ffmpeg.org/download.html) — colocar `ffmpeg.exe` en la misma carpeta (opcional, para filtros de audio)
- .NET Framework 4.x (incluido en Windows)

## Estructura de canciones

Las canciones van en la carpeta `songs/`, cada una en su propia subcarpeta con:
- Archivo `.txt` con letra y metadatos en formato UltraStar
- Archivo de video (`.mp4`, `.mkv`, etc.)
- Caratula opcional (`.jpg`, `.png`)

## Compilar

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /target:winexe ^
  /out:UltraStarSimplificado.exe ^
  /r:System.Windows.Forms.dll ^
  /r:System.Drawing.dll ^
  /r:System.dll ^
  /optimize+ ^
  UltraStarSimplificado.cs
```

## Controles

| Acción | Tecla |
|---|---|
| Navegar canciones | Flechas |
| Reproducir canción | Enter / Doble clic |
| Salir del karaoke | ESC |
| Buscar | Escribir directamente |
| Favorito | Clic en la estrella |

## Versión

v1.0 — primera versión pública.
