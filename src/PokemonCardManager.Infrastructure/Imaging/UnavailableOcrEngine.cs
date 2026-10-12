using PokemonCardManager.Core.Identification;

namespace PokemonCardManager.Infrastructure.Imaging;

/// <summary>Used where no OCR engine is registered (e.g. tests, non-Windows). Identification then relies on typed text.</summary>
public sealed class UnavailableOcrEngine : IOcrEngine
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<OcrLine>> RecognizeAsync(byte[] imageBytes, string language, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No OCR engine is available.");
}
