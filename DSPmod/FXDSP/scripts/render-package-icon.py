"""Export the approved icon master to the 256x256 release icon."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = root / 'image/icon/08-icon-final.png'
with Image.open(source) as image:
    image.convert('RGBA').resize((256, 256), Image.Resampling.LANCZOS).save(root / 'release/icon.png')
print('Exported release/icon.png (256x256) from image/icon/08-icon-final.png')
