# Testing Instructions

This document provides step-by-step instructions for testing the CSS style switching implementation.

## Prerequisites

- A modern web browser (Chrome, Firefox, Safari, Edge, or Opera)
- No additional dependencies or build tools required

## Testing Steps

### 1. Visual Testing

#### Test the Main Index Page
1. Open `index.html` in your web browser
2. Verify you see:
   - A page titled "Forms Repository - Home Page"
   - A blue info box explaining the style switching feature
   - A "Choose Your Style" section with 3 style options
   - An "Available Forms" section with 5 form links
3. Verify the page has minimal styling (light gray background, clean layout)

#### Test Style 1 - Professional Blue Theme
1. Click on "Style 1 - Professional Blue Theme" from the main index
2. Verify you see:
   - Purple/blue gradient background
   - White content boxes with rounded corners and shadows
   - Blue links (#667eea)
   - Blue left borders on list items
   - Smooth hover effects (items shift right slightly)
3. Verify the "Style Switcher Links" section appears at the bottom with proper spacing
4. Click on "Current Style (Professional Blue)" - should reload the same page
5. Click on "Switch to Style 2" - should navigate to Style 2

#### Test Style 2 - Modern Dark Theme
1. Navigate to `index-style2.html` (or use the style switcher)
2. Verify you see:
   - Dark background (#1a1a2e)
   - Light colored heading text (visible against dark background)
   - Green accent colors (#00ff88)
   - Green left borders on list items
   - Dark blue content boxes (#16213e)
   - Proper contrast for readability
3. Test the style switcher links at the bottom
4. Verify hover effects work (green glow on list items)

#### Test Style 3 - Warm Minimalist Theme
1. Navigate to `index-style3.html` (or use the style switcher)
2. Verify you see:
   - Cream/beige background (#fef5e7)
   - Orange/coral accent colors (#e67e22)
   - Serif font (Georgia)
   - Uppercase heading with underline
   - Clean, minimalist design
3. Test the style switcher links at the bottom
4. Verify hover effects work (items shift right on hover)

### 2. Navigation Testing

1. From any style page, click each form link to verify they work
2. Use browser back button to return to the style page
3. Switch between all three styles using the style switcher links
4. Verify each style maintains the same content but different visual presentation
5. Test the main index page links to ensure they navigate to correct style pages

### 3. Responsive Testing

1. Open each style page in your browser
2. Resize the browser window to various widths (mobile, tablet, desktop)
3. Verify content remains readable and properly formatted
4. Check that horizontal scrolling is not required
5. Verify buttons and links remain clickable on small screens

### 4. Consistency Testing

1. Open all three style HTML files side-by-side in a text editor
2. Verify they have identical HTML structure
3. Verify only the linked CSS file differs
4. Verify each has the appropriate comment indicating which style is used
5. Check that all files have proper DOCTYPE and meta tags

### 5. Code Quality Testing

1. Validate HTML files using W3C HTML Validator
2. Validate CSS files using W3C CSS Validator
3. Check for console errors in browser developer tools
4. Verify no inline styles exist in the HTML files (except main index.html)
5. Verify all CSS is in external files

### 6. Browser Compatibility Testing

Test all pages in multiple browsers:
- [ ] Chrome/Chromium
- [ ] Firefox
- [ ] Safari
- [ ] Edge
- [ ] Opera

Verify in each browser:
- Styles render correctly
- Colors appear as intended
- Gradients and shadows display properly
- Links and hover effects work
- No console errors

### 7. Accessibility Testing

1. Test keyboard navigation:
   - Tab through all links
   - Verify focus indicators are visible
   - Ensure all interactive elements are reachable

2. Test color contrast:
   - Use a contrast checker tool
   - Verify text is readable against backgrounds
   - Check that links have sufficient contrast

3. Test with screen readers (if available):
   - Verify content is announced in logical order
   - Check that links are properly described

## Expected Results

✅ All three style pages should display the same content with different visual themes
✅ Navigation between styles should be seamless
✅ No inline styles should exist in any index-style HTML file
✅ All CSS should be in external files in the `styles/` directory
✅ Pages should be responsive and work on different screen sizes
✅ No browser console errors
✅ All links should work correctly
✅ Hover and focus states should provide clear visual feedback

## Reporting Issues

If you find any issues during testing, please report:
1. Which style page (style1, style2, or style3)
2. What browser and version you're using
3. What you expected to see
4. What you actually saw
5. Steps to reproduce the issue
6. Screenshots if applicable

## Test cases (Quote form)

The quote form supports **Load test case** and **Clear form** when running in testing mode.

### Enabling testing mode

- Add `?testing=1` to the Index URL, or
- Run the app on `localhost`.

### Load test case

1. Use the **Load test case** dropdown (above the form).
2. Choose a scenario (e.g. *Flexo, Rectangle, Existing Die*).
3. The form is pre-filled. Finish, Cutting Die, and Materials may take a moment to load; the logic retries at 600 ms, 1.2 s, 2.5 s, and 3.5 s.
4. Adjust any fields as needed, then submit.

### Clear form

Click **Clear form** to reset the form to defaults (rectangle, rounded, existing die, etc.) and clear Printing, Finish, and Cutting Die.

### Updating test case IDs and display text

Test cases are defined in `wwwroot/data/test-cases.json`. API-dependent fields use IDs that can change per environment:

- **materialValue**: Material/Substrate `Id` from `/Api/Materials`.
- **material**: Display text or substring (e.g. `"Thermal"`) to match when `materialValue` is missing or not found; first option containing the substring is selected.
- **printingValue**: Color Code `Id` (e.g. `1F`, `2D`) from `/Api/ColorCodes`.
- **finishValue**: Finishing Type `Id` from `/Api/FinishingTypes` (filtered by Printing).
- **finish**: Display text or substring (e.g. `"Laminate"`, `"blank"`) to match when `finishValue` is missing or not found.
- **cuttingDieValue**: Cutting die option `value` (e.g. `stns_ref`) from `/Api/CuttingDie` for the selected Printing.

Edit `test-cases.json` and replace placeholders with IDs or display strings that exist in your APIs. **Phone** must use `111-222-3333` format (XXX-XXX-XXXX). Pre-fill retries at 600 ms, 1.2 s, 2.5 s, and 3.5 s so Material and Finish can load before selection.

---

## Additional Testing (Optional)

### Performance Testing
1. Open browser developer tools
2. Check page load times
3. Verify CSS files load quickly
4. Check for unnecessary network requests

### File Size Verification
Run the following command to check file sizes:
```bash
ls -lh styles/*.css
ls -lh index-style*.html
```

Expected sizes:
- CSS files: ~2.4-2.8KB each
- HTML files: ~1.1KB each
