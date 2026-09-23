using System.Collections.Generic;
using Uriangerizer.Chat;

namespace Uriangerizer.Pipeline;

/// <summary>A translation waiting for /uri send or /uri cancel.</summary>
public sealed record PendingPreview(ChannelPrefix Channel, IReadOnlyList<string> Parts);
