# Photos

## Importing: `Infrastructure/Images/ImageService.cs`
1. **Check** the file: it exists, is a supported type, isn't empty or too big.
2. **Verify the content.** `MetadataExtractor` reads the file header. A `.jpg` that's really a PNG is rejected, because the extension alone can lie.
3. **Copy** the bytes unchanged into the images folder while computing a **SHA-256 hash** (a fingerprint that detects duplicates and corruption). The original is never re-compressed.
4. **Save** a `CardImage` row with the path, size, hash and capture date ([[Data Model]]).

## Where files live: `ImageFileStore.cs`
The database stores a **storage key** like `2026/10/0199...jpg`, relative to the images folder. `GetFullPath` turns it into a real path and **refuses** any key that tries to escape the folder (e.g. `..\..\Windows`). This protects against bad data pointing at other files.

## Showing thumbnails: `App/Imaging/ThumbnailLoader.cs`
```csharp
image.DecodePixelWidth = decodeWidth; // decode straight to small size
image.Freeze();                       // safe to use from another thread
```
A 20-megapixel photo is decoded directly to 64 pixels wide, so the list doesn't eat memory. A broken file shows no thumbnail instead of crashing.

`CardRowViewModel` only loads a thumbnail the first time the screen asks for it, and the XAML binds with `IsAsync=True`:
```xml
<Image Source="{Binding Thumbnail, IsAsync=True}" />
```
`IsAsync` tells WPF to fetch the value on a background thread, so scrolling never freezes while pictures load.

## Originals vs derivatives
`CardImage.OriginalImageId` is empty for an original photo. Later, crops and annotated versions will point back at their original, and the original can't be deleted while they exist.
