import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path


AUDIO_EXTS = {".mp3", ".m4a", ".wav", ".ogg", ".flac", ".aac"}


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
        if path.is_file() and path.suffix.lower() in AUDIO_EXTS and path.name.lower() not in {"instrumental.mp3", "karaoke.mp3", "no_vocals.mp3"}:
            return path
    return None


def demucs_device() -> str:
    try:
        import torch

        return "cuda" if torch.cuda.is_available() else "cpu"
    except Exception:
        return "cpu"


def run_demucs(audio: Path, work_dir: Path) -> Path:
    device = demucs_device()
    jobs = max(1, min(4, (os.cpu_count() or 2) // 2))
    print(f"Demucs device: {device} | jobs: {jobs}", flush=True)
    cmd = [
        sys.executable,
        "-m",
        "demucs",
        "--two-stems=vocals",
        "-n",
        "htdemucs",
        "-o",
        str(work_dir),
        "-d",
        device,
        "-j",
        str(jobs),
        str(audio),
    ]
    print(" ".join(f'"{part}"' if " " in part else part for part in cmd), flush=True)
    subprocess.run(cmd, check=True)
    separated_root = work_dir / "htdemucs" / audio.stem
    instrumental = separated_root / "no_vocals.wav"
    if not instrumental.exists():
        raise FileNotFoundError(f"No se genero {instrumental}")
    return instrumental


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("songs", nargs="?", default="songs")
    args = parser.parse_args()

    songs_root = Path(args.songs).resolve()
    if not songs_root.exists():
        print(f"No existe la carpeta: {songs_root}")
        return 1

    if shutil.which("ffmpeg") is None:
        local_ffmpeg = Path(__file__).with_name("ffmpeg.exe")
        if local_ffmpeg.exists():
            print(f"Usando ffmpeg local: {local_ffmpeg}")
        else:
            print("Aviso: no encontre ffmpeg en PATH ni junto al script. Demucs puede necesitarlo.")

    song_dirs = [p for p in songs_root.iterdir() if p.is_dir()]
    print(f"Canciones encontradas: {len(song_dirs)}", flush=True)

    for index, song_dir in enumerate(song_dirs, 1):
        done_marker = song_dir / ".karaoke_preprocesado.ok"
        output = song_dir / "instrumental.mp3"
        if output.exists() and done_marker.exists():
            print(f"[{index}/{len(song_dirs)}] Ya existe instrumental: {song_dir.name}", flush=True)
            continue

        audio = find_audio(song_dir)
        if audio is None:
            print(f"[{index}/{len(song_dirs)}] Sin audio: {song_dir.name}", flush=True)
            continue

        print(f"[{index}/{len(song_dirs)}] Procesando: {song_dir.name}", flush=True)
        work_dir = song_dir / "_demucs"
        try:
            instrumental_wav = run_demucs(audio, work_dir)
            ffmpeg = Path(__file__).with_name("ffmpeg.exe")
            ffmpeg_cmd = [str(ffmpeg if ffmpeg.exists() else "ffmpeg"), "-y", "-threads", "0", "-i", str(instrumental_wav), "-codec:a", "libmp3lame", "-q:a", "2", str(output)]
            subprocess.run(ffmpeg_cmd, check=True)
            done_marker.write_text("ok\n", encoding="utf-8")
            print(f"Guardado: {output}", flush=True)
        except Exception as exc:
            print(f"ERROR en {song_dir.name}: {exc}", flush=True)

    print("Listo.", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
