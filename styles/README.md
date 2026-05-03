# Styles Directory

This directory contains CSS stylesheets for the forms repository.

## Available Stylesheets

### style-arial.css

A comprehensive CSS stylesheet that uses **Arial** as the primary font family with proper fallbacks.

**Font-Family Chain:**
```css
font-family: Arial, Helvetica, sans-serif;
```

**Features:**
- Arial font set for all HTML elements (html, body, headings, paragraphs, forms, etc.)
- Proper fallback fonts: Helvetica and sans-serif
- Comprehensive styling for common HTML elements
- Form-specific classes for compatibility with existing forms
- Utility classes for text sizing and weight
- Uses rem units for better scalability
- Well-documented and follows CSS best practices

**Usage:**

To use this stylesheet in your HTML files, add the following link tag in the `<head>` section:

```html
<link rel="stylesheet" href="styles/style-arial.css">
```

**Example:**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>My Form</title>
  <link rel="stylesheet" href="styles/style-arial.css">
</head>
<body>
  <h1>Welcome</h1>
  <p>This text will be displayed in Arial font.</p>
</body>
</html>
```

## Best Practices

- Always include fallback fonts for better cross-browser compatibility
- Use relative units (rem, em) instead of absolute units (px) when possible
- Keep CSS well-organized with clear comments
- Follow a consistent naming convention for classes
- Group related styles together

## Browser Support

The Arial font is widely supported across all major browsers and operating systems, making it an excellent choice for web development. The fallback chain ensures graceful degradation on systems where Arial is not available.
