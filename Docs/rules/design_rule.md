# UI Design Specification

> Version: 1.0  
> Created: 2026-02-22  
> Target: Unity uGUI (Canvas + TextMeshPro)  
> Purpose: **Design directive for AI agents** — Follow this document's specifications strictly

---

## 0. Instructions for AI

```
You are an engineer implementing UI according to this design specification.
Strictly follow the rules below.

1. Use only the values defined in this specification. Do not change colors, sizes, or spacing at your own discretion
2. Do not make unsolicited "improvements" that you think might be better. Do not add anything not in the spec
3. More whitespace is always correct. When in doubt, go wider
4. Before adding any element, ask yourself "Is this truly necessary?" — if not, do not add it
5. Only two font weights are allowed (Regular / SemiBold). Do not use Bold, Light, etc.
6. Decorative shadows, gloss, textures, and gradients are prohibited
7. Corner radius must be uniform across all elements. Do not vary it per element
8. Do not use Pure Black (#000000) or Pure White (#FFFFFF)
```

---

## 1. Design Philosophy

The UI for this project follows the principles below. When in doubt, return here.

| Principle | Description | Practice |
|---|---|---|
| **Less is More** | Strip away every unnecessary element | "Does it still work if I remove this?" → If yes, remove it |
| **Power of Empty Space** | Whitespace is the star of the design | Do not cram elements; ensure breathing room |
| **Honest Design** | Do not disguise with decoration | No shadows, gloss, or gradients. Show the material as-is |
| **Function = Form** | Every element has a role | Remove any element that is purely decorative |
| **Consistency** | Same rules across all screens | Do not create unique styles per screen |
| **User-Centered** | Minimize cognitive load | One concept per screen. Progressive Disclosure |

---

## 2. Color System

### 2.1 Palette Definition

Pure Black / Pure White are prohibited. Use off-tone values throughout for softness.

| Token | Hex | Unity Color (RGBA 0-1) | Usage |
|---|---|---|---|
| `bg-primary` | `#F7F7F8` | `(0.969, 0.969, 0.973, 1.0)` | Main background |
| `bg-secondary` | `#EDEDF0` | `(0.929, 0.929, 0.941, 1.0)` | Card / panel background |
| `bg-tertiary` | `#E2E2E7` | `(0.886, 0.886, 0.906, 1.0)` | Divider lines / separators |
| `surface` | `#FFFFFF` | `(1.0, 1.0, 1.0, 1.0)` | Elevated card surface ※ Pure White allowed as exception |
| `text-primary` | `#1D1D1F` | `(0.114, 0.114, 0.122, 1.0)` | Main text |
| `text-secondary` | `#6E6E73` | `(0.431, 0.431, 0.451, 1.0)` | Secondary text / labels |
| `text-tertiary` | `#AEAEB2` | `(0.682, 0.682, 0.698, 1.0)` | Placeholder / disabled text |
| `accent` | `#0A84FF` | `(0.039, 0.518, 1.0, 1.0)` | Primary action / links / selected state |
| `accent-hover` | `#0070E0` | `(0.0, 0.439, 0.878, 1.0)` | Hover / press state of `accent` |
| `success` | `#30D158` | `(0.188, 0.820, 0.345, 1.0)` | Success / save complete |
| `warning` | `#FF9F0A` | `(1.0, 0.624, 0.039, 1.0)` | Warning / caution |
| `error` | `#FF453A` | `(1.0, 0.271, 0.227, 1.0)` | Error / deletion |
| `divider` | `#D1D1D6` | `(0.820, 0.820, 0.839, 1.0)` | Thin divider lines |

### 2.2 Color Rules

- **Base: Neutral (gray family) only.** 90%+ of surface area must be neutral colors
- **Accent: One color only (`accent`).** Use it only where it carries meaning
- **Semantic colors:** Colors carry meaning. Using color purely for decoration is prohibited
  - Blue = Action / Selection
  - Green = Success
  - Yellow = Warning
  - Red = Error / Deletion
- **Contrast:** Text must meet WCAG AA (4.5:1) or higher against its background
- **Grayscale test:** The UI must remain functionally usable when all colors are converted to grayscale

---

## 3. Typography

### 3.1 Font

| Usage | Font | Fallback |
|---|---|---|
| All text | **Noto Sans JP** | sans-serif |

> Only **one** font family is allowed. Using a second font is prohibited.

### 3.2 Type Scale

Based on an 8pt baseline.

| Level | fontSize | fontWeight | lineHeight ratio | Usage |
|---|---|---|---|---|
| `display` | 32 | SemiBold (600) | 1.3 | Screen title (used extremely sparingly) |
| `heading` | 20 | SemiBold (600) | 1.4 | Section heading |
| `subheading` | 16 | SemiBold (600) | 1.4 | Card title / label |
| `body` | 14 | Regular (400) | 1.6 | Body text / descriptions |
| `caption` | 12 | Regular (400) | 1.5 | Supporting info / timestamps |
| `micro` | 10 | Regular (400) | 1.4 | Badges / very small labels |

### 3.3 Typography Rules

- **Only two weights allowed:** Regular (400) and SemiBold (600)
- **Long text is left-aligned.** Center alignment is restricted to short titles or labels
- **Letter spacing (letterSpacing):** 0 (default). Adjustments are prohibited
- **Full-width alphanumerics are prohibited.** Always use half-width for letters and numbers
- Use TextMeshPro `SDF Font Asset`. Runtime fonts are prohibited

---

## 4. Spacing System

### 4.1 Base Unit: 4px

All margins, padding, and gaps must be **multiples of 4**.

| Token | Value | Primary Usage |
|---|---|---|
| `space-none` | 0 | No gap |
| `space-xs` | 4 | Between icon and label |
| `space-sm` | 8 | Between related elements (within the same group) |
| `space-md` | 16 | Card inner padding / standard gap between elements |
| `space-lg` | 24 | Separation within a section |
| `space-xl` | 32 | Between sections |
| `space-2xl` | 48 | Between major sections / top-bottom screen margins |

### 4.2 Spacing Rules

- **When in doubt, choose the wider option.** More whitespace = correct
- **Gestalt's Law of Proximity:** Related items are closer; unrelated items are farther apart
- Between elements in the same group: `space-sm` (8)
- Between different groups: `space-lg` (24) or more
- Panel inner padding: at least `space-md` (16)

---

## 5. Layout

### 5.1 Structural Principles

- **One concept per screen.** Do not cram multiple unrelated features into a single screen
- **Make Visual Hierarchy clear.** Express importance through size, color, and position
- **One primary action per screen.** Only one prominent button
- **Progressive Disclosure:** Show only what is needed upfront; reveal details on expand / hover

### 5.2 Grid and Alignment

- **Left-aligned by default.** Align all elements to the left edge
- **Pixel-perfect:** All elements must snap to the grid. Misalignment of 2px or more is prohibited
- Unity uGUI: compose with `VerticalLayoutGroup` / `HorizontalLayoutGroup`
  - `childForceExpandWidth = false`
  - `childForceExpandHeight = false`
  - `childControlWidth = true`
  - `childControlHeight = true`

### 5.3 Canvas Scaler

```
UI Scale Mode: Scale With Screen Size
Reference Resolution: 1920 x 1080
Screen Match Mode: Match Width Or Height
Match: 0.5
```

---

## 6. Component Specifications

### 6.1 Buttons

| Property | Primary Button | Secondary Button | Danger Button | Ghost Button |
|---|---|---|---|---|
| Height | 40 | 40 | 40 | 40 |
| Min width | 80 | 80 | 80 | - |
| Horizontal padding | 20 | 20 | 20 | 12 |
| Background color | `accent` | `bg-secondary` | `error` | Transparent |
| Text color | `#FFFFFF` | `text-primary` | `#FFFFFF` | `accent` |
| fontSize | 14 | 14 | 14 | 14 |
| fontWeight | SemiBold | Regular | SemiBold | Regular |
| Corner radius | `corner-radius` | `corner-radius` | `corner-radius` | 0 |
| Hover | `accent-hover` | `bg-tertiary` | `#E03E35` | `bg-secondary` |
| Press | `#005EC4` | `divider` | `#C43530` | `bg-tertiary` |
| Disabled | opacity 0.4 | opacity 0.4 | opacity 0.4 | opacity 0.4 |

### 6.2 Cards

| Property | Value |
|---|---|
| Background color | `surface` |
| Padding | `space-md` (16) |
| Corner radius | `corner-radius` |
| Shadow | **None** |
| Border | 1px `divider` (optional; substitute for shadow) |
| Element gap | `space-sm` (8) |

### 6.3 Input Fields

| Property | Value |
|---|---|
| Height | 40 |
| Background color | `bg-primary` |
| Text color | `text-primary` |
| Placeholder color | `text-tertiary` |
| Border (default) | 1px `divider` |
| Border (focus) | 2px `accent` |
| Border (error) | 2px `error` |
| Corner radius | `corner-radius` |
| Horizontal padding | 12 |
| fontSize | 14 |

### 6.4 Dropdowns

| Property | Value |
|---|---|
| Trigger height | 40 |
| Trigger background | `bg-secondary` |
| Expanded background | `surface` |
| Option height | 36 |
| Option hover | `bg-secondary` |
| Selected | `accent` background + `#FFFFFF` text |
| Corner radius | `corner-radius` |
| Border | 1px `divider` |
| Drop shadow | **None** |

### 6.5 Status Badges

| Property | Value |
|---|---|
| Height | 24 |
| Horizontal padding | 8 |
| Corner radius | 12 (pill shape) |
| fontSize | 12 |
| fontWeight | SemiBold |
| Success | `success` background (opacity 0.15) + `success` text |
| Warning | `warning` background (opacity 0.15) + `warning` text |
| Error | `error` background (opacity 0.15) + `error` text |
| Info | `accent` background (opacity 0.15) + `accent` text |

### 6.6 Dividers

| Property | Value |
|---|---|
| Height | 1 |
| Color | `divider` |
| Horizontal margin | Indented by `space-md` (16), or full-bleed |

### 6.7 Icons

| Property | Value |
|---|---|
| Size | 20 × 20 (standard) / 16 × 16 (small) / 24 × 24 (large) |
| Color | `text-secondary` (default) / `text-primary` (emphasis) |
| Style | **Stroke (outline) icons only.** Mixing fill and stroke icons is prohibited |
| Stroke width | Uniform (1.5 – 2) |
| Interaction | Change color to `text-primary` on hover |

---

## 7. Shared Values

| Token | Value | Notes |
|---|---|---|
| `corner-radius` | 8 | **Uniform across all components.** Do not vary per element |
| `transition-duration` | 150ms | State transitions such as hover / press |
| `min-touch-target` | 44 × 44 | Minimum size for touch / click targets |

---

## 8. Interactions

### 8.1 State Expression

| State | Expression |
|---|---|
| Default | Normal display |
| Hover | Darken background by one step. Color change is smooth using `transition-duration` |
| Press | Darken background by one additional step |
| Focus | 2px `accent` border |
| Disabled | opacity 0.4. Non-clickable |
| Selected | `accent`-colored indicator (left border or tinted background) |
| Error | `error`-colored border + error message at `caption` size |

### 8.2 Animations

- **Subtle and natural only.** Excessive animation is prohibited
- **Allowed:**
  - Background color fade on hover / press (150ms ease)
  - Panel open / close (200ms ease-out)
  - Status message fade-in / fade-out (200ms)
- **Prohibited:**
  - Bounce animations
  - Rotation animations
  - Particle effects
  - Scale animations (e.g., button growing larger)
  - Any animation longer than 300ms

---

## 9. Dark Mode (Future Extension)

Currently light mode only. Color mapping reference for future dark mode support:

| Light Mode | Dark Mode |
|---|---|
| `bg-primary` #F7F7F8 | `#1C1C1E` |
| `bg-secondary` #EDEDF0 | `#2C2C2E` |
| `surface` #FFFFFF | `#3A3A3C` |
| `text-primary` #1D1D1F | `#F5F5F7` |
| `text-secondary` #6E6E73 | `#98989D` |

---

## 10. Prohibition Checklist

After implementing UI, verify all of the following. If even one item applies, fix it.

- [ ] Is Pure Black (#000000) used anywhere?
- [ ] Is Pure White (#FFFFFF) used for backgrounds (other than `surface` cards)?
- [ ] Are three or more font weights used?
- [ ] Are decorative shadows (drop shadows) used?
- [ ] Are gradients used?
- [ ] Are two or more accent colors used?
- [ ] Does corner radius vary between elements?
- [ ] Are any spacing values not multiples of 4?
- [ ] Are there any purely decorative elements with no functional purpose?
- [ ] Are there two or more primary action buttons on a single screen?
- [ ] Is there center-aligned long-form text?
- [ ] Is any text-to-background contrast ratio below 4.5:1?
- [ ] Are there any animations longer than 300ms?
- [ ] Are fill icons and stroke icons mixed?
- [ ] Are any font sizes used that are not defined in the Type Scale?
