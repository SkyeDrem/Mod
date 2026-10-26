"""Read-only validation of the production atlas. Does not write or edit images."""
from pathlib import Path
import hashlib
from PIL import Image

path = Path(__file__).resolve().parents[1] / 'src/FXDSP.DeliveryBootstrap/Assets/technology-icons.png'
with Image.open(path) as image:
    assert image.mode == 'RGBA', 'Technology icons require real RGBA transparency.'
    width, height = image.size
    assert width % 4 == 0 and height % 3 == 0 and width // 4 == height // 3
    assert image.getchannel('A').getextrema() == (0, 255)
    pixels = image.load()
    side = width // 4
    names = ['1951 access'] + [f'{7700 + level} exchange {level}' for level in range(1, 6)] + [f'{7800 + level} efficiency {level}' for level in range(1, 7)]
    for index, name in enumerate(names):
        left = index % 4 * side
        top = index // 4 * side
        bounds = [side, side, -1, -1]
        visible = opaque = 0
        for y in range(side):
            for x in range(side):
                r, g, b, a = pixels[left + x, top + y]
                brightness = max(r, g, b)
                alpha = a * (255 if brightness >= 224 else brightness) // 255
                if alpha <= 8: alpha = 0
                elif alpha >= 248: alpha = 255
                if alpha > 32:
                    bounds[0] = min(bounds[0], x); bounds[1] = min(bounds[1], y)
                    bounds[2] = max(bounds[2], x); bounds[3] = max(bounds[3], y)
                    visible += 1
                if alpha == 255: opaque += 1
        assert .05 < visible / side ** 2 < .65, f'Empty or contaminated cell {name}.'
        assert opaque > side ** 2 * .04, f'Icon is too faint: {name}.'
        assert 0 < bounds[0] <= bounds[2] < side - 1 and 0 < bounds[1] <= bounds[3] < side - 1, f'Cell clips or bleeds: {name}.'
        extent = max(bounds[2] - bounds[0] + 5, bounds[3] - bounds[1] + 5)
        size = extent + 2 * max(8, (extent + 11) // 12)
        assert 128 <= size <= 512, f'Unexpected imported sprite size: {name}.'
        print(f'PASS {name}: source bounds {bounds}; {size}px padded mask -> native 80px sprite')
print(f'RGBA atlas {width}x{height}, twelve independent sprites; SHA256 {hashlib.sha256(path.read_bytes()).hexdigest()}')
