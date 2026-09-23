using System;
using System.Threading;
using System.Threading.Tasks;

namespace Uriangerizer.Translation;

public interface ITranslator
{
    /// <summary>Returns the raw model output. Throws <see cref="TranslationException"/> on any failure.</summary>
    Task<string> TranslateAsync(string text, CancellationToken cancellationToken);
}

/// <summary>A failure whose Message is safe to print in the local chat (never contains the API key).</summary>
public sealed class TranslationException(string message, Exception? inner = null) : Exception(message, inner);
