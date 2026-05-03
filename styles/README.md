# Styles Directory

This directory contains CSS stylesheets for the forms repository.

## Files

### style1.css

Primary stylesheet that implements Roboto font as the default font family for the application.

**Features:**
- Imports Roboto font from Google Fonts (multiple weights: 300, 400, 500, 700)
- Applies Roboto to the entire HTML document (body, headings, form elements)
- Includes comprehensive fallback font stack for cross-platform compatibility
- Provides utility classes for different font weights
- Uses `font-display: swap` for optimal loading performance

**Usage:**

Add the following line to the `<head>` section of your HTML file:

```html
<link rel="stylesheet" href="styles/style1.css">
```

**Example:**

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>My Form</title>
    <link rel="stylesheet" href="styles/style1.css">
</head>
<body>
    <h1>Welcome</h1>
    <p>This text will use the Roboto font.</p>
    <p class="font-bold">This text will be bold.</p>
</body>
</html>
```

**Font Weight Utility Classes:**

- `.font-light` - Weight 300 (Light)
- `.font-regular` - Weight 400 (Regular)
- `.font-medium` - Weight 500 (Medium)
- `.font-bold` - Weight 700 (Bold)

**Fallback Strategy:**

The font-family declaration includes multiple fallbacks:
1. `Roboto` - Primary font from Google Fonts
2. `-apple-system` - Native system font for macOS/iOS
3. `BlinkMacSystemFont` - System font for Chrome on macOS
4. `Segoe UI` - System font for Windows
5. `Arial` - Widely available sans-serif
6. `sans-serif` - Generic fallback

This ensures the best possible rendering even if Google Fonts is unavailable.

## Browser Compatibility

The Roboto font and CSS implementation is compatible with all modern browsers:
- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Opera (latest)

## Performance

The stylesheet uses Google Fonts API with `display=swap` parameter, which ensures:
- Text remains visible during font loading (FOIT prevention)
- Improved perceived performance
- Better user experience on slow connections
