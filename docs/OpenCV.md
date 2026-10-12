# OpenCV

**OpenCV** is a free library of image-processing tools. The app uses it through **OpenCvSharp**, a C# wrapper. It lives only in Infrastructure, behind Core interfaces (`IImageAnalyzer`, `ICardDetector`), so the rest of the app never touches it.

## Key ideas
- A picture is a **`Mat`**: a grid of numbers (one per pixel for greyscale, three for colour).
- Most steps take one `Mat` and write a new one. The photo file is **never changed**.
- `Mat` holds unmanaged memory, so it's wrapped in `using` to free it promptly.

## Operations used
| Call | What it does | Used for |
|---|---|---|
| `ImDecode` | Turn file bytes into a `Mat` | everything |
| `Resize` | Shrink | consistent measurements, speed |
| `Laplacian` + `MeanStdDev` | Edge strength, and how much it varies | sharpness ([[Inspection]]) |
| `InRange` + `CountNonZero` | Count pixels in a brightness range | glare / dark spots |
| `GaussianBlur` | Soften, so paper texture isn't an "edge" | card detection |
| `Canny` | Find edges | card detection |
| `FindContours` + `ApproxPolyDP` | Outlines, simplified to corners | find the 4-cornered card |
| `GetPerspectiveTransform` + `WarpPerspective` | Map 4 corners onto a flat rectangle | straighten a tilted card ([[Card Identification]]) |

## Two practical tricks in the code
- Files are read into memory with `File.ReadAllBytesAsync` first, because OpenCV's own file reading fails on Windows paths with letters like "ø".
- Pixel work runs inside `Task.Run`, so the window never freezes.
