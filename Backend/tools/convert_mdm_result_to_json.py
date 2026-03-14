#!/usr/bin/env python3
import argparse
import json
from pathlib import Path

import numpy as np


def main() -> None:
    parser = argparse.ArgumentParser(description="Convert MDM results.npy to Unity-friendly JSON")
    parser.add_argument("--input", default="Assets/results.npy", help="Path to MDM results.npy")
    parser.add_argument("--output", default="Assets/Resources/Motions/result_motion.json", help="Output JSON path")
    parser.add_argument("--sample-index", type=int, default=0, help="Sample index in motion batch")
    parser.add_argument("--fps", type=float, default=20.0, help="Motion FPS")
    args = parser.parse_args()

    input_path = Path(args.input)
    output_path = Path(args.output)

    raw = np.load(input_path, allow_pickle=True)
    if not isinstance(raw, np.ndarray) or raw.dtype != object:
        raise ValueError(f"Unsupported npy layout: dtype={getattr(raw, 'dtype', None)}")

    payload = raw.item()
    if not isinstance(payload, dict):
        raise ValueError("Expected a dict payload in npy")

    motion = payload.get("motion")
    texts = payload.get("text", [])
    lengths = payload.get("lengths", [])

    if not isinstance(motion, np.ndarray) or motion.ndim != 4:
        raise ValueError(f"Expected motion shape [N, 22, 3, T], got {type(motion)} / {getattr(motion, 'shape', None)}")

    n_samples = motion.shape[0]
    idx = max(0, min(args.sample_index, n_samples - 1))

    sample = motion[idx].astype(np.float32)  # [22, 3, T]
    sample_ft = np.transpose(sample, (2, 0, 1))  # [T, 22, 3]

    frames = int(sample_ft.shape[0])
    joints = int(sample_ft.shape[1])
    flat = sample_ft.reshape(-1).tolist()

    text = ""
    if isinstance(texts, list) and idx < len(texts):
        text = str(texts[idx])

    length = frames
    if isinstance(lengths, np.ndarray) and idx < len(lengths):
        length = int(lengths[idx])

    out = {
        "version": 1,
        "source": "mdm_results_npy",
        "sampleIndex": idx,
        "frames": frames,
        "joints": joints,
        "fps": float(args.fps),
        "length": length,
        "text": text,
        "positions": flat,
    }

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(out, ensure_ascii=False), encoding="utf-8")
    print(f"Wrote {output_path} | sample={idx} | shape=[{frames}, {joints}, 3]")


if __name__ == "__main__":
    main()
