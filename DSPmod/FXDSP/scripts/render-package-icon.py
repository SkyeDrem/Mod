"""Rasterize the package's authored SVG primitives without external game assets."""
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
svg = ET.parse(root / 'release/icon.svg').getroot()
scale = 4
image = Image.new('RGBA', (256 * scale, 256 * scale))
draw = ImageDraw.Draw(image)
for element in svg:
    tag = element.tag.split('}')[-1]
    a = element.attrib
    width = max(1, round(float(a.get('stroke-width', '1')) * scale))
    stroke = a.get('stroke')
    fill = a.get('fill')
    if fill == 'none':
        fill = None
    if tag in ('polygon', 'polyline'):
        points = [tuple(float(c) * scale for c in p.split(',')) for p in a['points'].split()]
        if tag == 'polygon':
            draw.polygon(points, fill=fill)
            points.append(points[0])
        if stroke:
            draw.line(points, fill=stroke, width=width, joint='curve')
    elif tag == 'line':
        draw.line(tuple(float(a[key]) * scale for key in ('x1', 'y1', 'x2', 'y2')), fill=stroke, width=width)
    elif tag == 'rect':
        x, y, w, h = (float(a[key]) * scale for key in ('x', 'y', 'width', 'height'))
        draw.rectangle((x, y, x + w, y + h), fill=fill)
    elif tag == 'text':
        font = ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf', round(float(a['font-size']) * scale))
        draw.text((float(a['x']) * scale, float(a['y']) * scale), element.text, font=font, fill=fill, anchor='ms')
    else:
        raise ValueError('Unsupported package icon primitive: ' + tag)
image.resize((256, 256), Image.Resampling.LANCZOS).save(root / 'release/icon.png')
print('Rendered release/icon.png (256x256) from release/icon.svg')
