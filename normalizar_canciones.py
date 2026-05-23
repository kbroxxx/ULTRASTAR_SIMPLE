import argparse
import shutil
import subprocess
from pathlib import Path


AUDIO_EXTS = {".mp3", ".m4a", ".wav", ".ogg", ".flac", ".aac"}
SKIP_NAMES = {
    "audio_normalizado.mp3",
    "instrumental_normalizado.mp3",
    "instrumental.mp3",
    "karaoke.mp3",
    "no_vocals.mp3",
}


def read_text(path: Path) -> str:
    data = path.read_bytes()
    for encoding in ("utf-8-sig", "utf-8", "cp1252"):
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            pass
    return data.decode("cp1252", errors="replace")


def find_audio(song_dir: Path) -> Path | None:
    txt_files = list(song_dir.glob("*.txt"))
    if txt_files:
        for line in read_text(txt_files[0]).splitlines():
            if line.upper().startswith("#MP3:"):
                candidate = song_dir / line.split(":", 1)[1].strip()
                if candidate.exists():
                    return candidate
    for path in song_dir.iterdir():
        if path.is_file() and path.suffix.lower() in AUDIO_EXTS and path.name.lower() not in SKIP_NAMES:
            return path
    return None


def normalize(ffmpeg: str, source: Path, output: Path) -> None:
    cmd = [
        ffmpeg,
        "-y",
        "-threads",
        "0",
        "-i",
        str(source),
        "-af",
        "loudnorm=I=-16:TP=-1.5:LRA=11",
        "-codec:a",
        "libmp3lame",
        "-q:a",
        "2",
        str(output),
    ]
    subprocess.run(cmd, check=True)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("songs", nargs="?", default="songs")
    args = parser.parse_args()

    songs_root = Path(args.songs).resolve()
    if not songs_root.exists():
        print(f"No existe la carpeta: {songs_root}")
        return 1

    local_ffmpeg = Path(__file__).with_name("ffmpeg.exe")
    ffmpeg = str(local_ffmpeg if local_ffmpeg.exists() else "ffmpeg")
    if not local_ffmpeg.exists() and shutil.which("ffmpeg") is None:
        print("No encontre ffmpeg.exe junto al script ni en PATH.")
        return 1

    song_dirs = [p for p in songs_root.iterdir() if p.is_dir()]
    print(f"Canciones encontradas: {len(song_dirs)}", flush=True)

    for index, song_dir in enumerate(song_dirs, 1):
        marker = song_dir / ".normalizado.ok"
        output = song_dir / "audio_normalizado.mp3"
        instrumental = song_dir / "instrumental.mp3"
        instrumental_output = song_dir / "instrumental_normalizado.mp3"

        if marker.exists() and output.exists() and (not instrumental.exists() or instrumental_output.exists()):
            print(f"[{index}/{len(song_dirs)}] Ya normalizada: {song_dir.name}", flush=True)
            continue

        audio = find_audio(song_dir)
        if audio is None:
            print(f"[{index}/{len(song_dirs)}] Sin audio: {song_dir.name}", flush=True)
            continue

        print(f"[{index}/{len(song_dirs)}] Normalizando audio: {song_dir.name}", flush=True)
        try:
            if not output.exists():
                normalize(ffmpeg, audio, output)
                print(f"Guardado: {output.name}", flush=True)
            if instrumental.exists() and not instrumental_output.exists():
                print(f"Normalizando instrumental: {song_dir.name}", flush=True)
                normalize(ffmpeg, instrumental, instrumental_output)
                print(f"Guardado: {instrumental_output.name}", flush=True)
            marker.write_text("ok\n", encoding="utf-8")
        except Exception as exc:
            print(f"ERROR en {song_dir.name}: {exc}", flush=True)

    print("Listo.", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
