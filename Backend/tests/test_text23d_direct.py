import sys
import os
import argparse
from pathlib import Path

# Add project root to path
sys.path.append(str(Path(__file__).parent.parent.parent))

from Backend.services.text23d import generate_sync

def main():
    parser = argparse.ArgumentParser(description="Test Direct Text-to-3D Generation")
    parser.add_argument("prompt", type=str, help="Text prompt for 3D model generation")
    parser.add_argument("--format", type=str, default="glb", help="Output format (glb, obj, fbx)")
    
    args = parser.parse_args()
    
    print(f"Testing Direct Text-to-3D generation with prompt: '{args.prompt}'")
    
    try:
        model_data = generate_sync(args.prompt, args.format)
        print(f"Success! Generated model size: {len(model_data)} bytes")
        
        # Save to a test file
        output_file = Path(f"test_output.{args.format}")
        output_file.write_bytes(model_data)
        print(f"Saved to: {output_file.absolute()}")
        
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()
