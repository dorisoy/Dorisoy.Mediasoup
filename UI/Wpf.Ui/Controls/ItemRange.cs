

/* Based on VirtualizingWrapPanel created by S. Bäumlisberger licensed under MIT license.
   https://github.com/sbaeumlisberger/VirtualizingWrapPanel

   Copyright (C) S. Bäumlisberger
   All Rights Reserved. */

namespace Wpf.Ui.Controls;

/// <summary>
/// Items range.
/// <para>Based on <see href="https://github.com/sbaeumlisberger/VirtualizingWrapPanel"/>.</para>
/// </summary>
public readonly struct ItemRange
{
    public int StartIndex { get; }

    public int EndIndex { get; }

    public ItemRange(int startIndex, int endIndex)
        : this()
    {
        StartIndex = startIndex;
        EndIndex = endIndex;
    }

    public readonly bool Contains(int itemIndex) => itemIndex >= StartIndex && itemIndex <= EndIndex;
}
