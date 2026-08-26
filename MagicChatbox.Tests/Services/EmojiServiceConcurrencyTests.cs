using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Services;

/// <summary>
/// The emoji service is read from the message build and written from the options page.
/// </summary>
/// <remarks>
/// The emoji box rewrites the whole collection on every keystroke - the text binding clears it and
/// refills it per character - while the once-a-second message build asks the same service for its
/// next icon. Shuffling walked the live collection, so a build landing between the clear and the
/// refill threw "collection was modified", and the shuffled queue was dequeued from two threads with
/// nothing guarding it. Both are why the build could not leave the UI thread.
/// </remarks>
public class EmojiServiceConcurrencyTests
{
    [Fact]
    public async Task Reading_an_icon_survives_the_collection_being_rewritten_underneath_it()
    {
        var settings = new AppSettings { EnableEmojiShuffle = true, EnableEmojiShuffleInChats = true };
        settings.EmojiCollection.Add("a");
        settings.EmojiCollection.Add("b");
        settings.EmojiCollection.Add("c");

        var service = new EmojiService(settings);
        var failures = new List<Exception>();
        using var done = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // One writer, standing in for the options page; two readers, for the build and the chat path.
        Task writer = Task.Run(() =>
        {
            try
            {
                while (!done.IsCancellationRequested)
                    service.EmojiListString = "x,y,z,w";
            }
            catch (Exception ex)
            {
                lock (failures) failures.Add(ex);
            }
        });

        Task[] readers = new Task[2];
        for (int i = 0; i < readers.Length; i++)
        {
            bool isChat = i == 1;
            readers[i] = Task.Run(() =>
            {
                try
                {
                    while (!done.IsCancellationRequested)
                        Assert.False(string.IsNullOrEmpty(service.GetNextEmoji(isChat)));
                }
                catch (Exception ex)
                {
                    lock (failures) failures.Add(ex);
                }
            });
        }

        await Task.WhenAll([writer, .. readers]);

        Assert.True(failures.Count == 0, "concurrent access threw: " + string.Join(" | ", failures));
    }

    [Fact]
    public void An_emptied_collection_falls_back_instead_of_throwing()
    {
        var settings = new AppSettings { EnableEmojiShuffle = true };
        settings.EmojiCollection.Add("a");

        var service = new EmojiService(settings);
        _ = service.GetNextEmoji();

        settings.EmojiCollection.Clear();

        Assert.False(string.IsNullOrEmpty(service.GetNextEmoji()));
        Assert.Empty(service.EmojiSnapshot);
    }

    [Fact]
    public void Replacing_the_collection_wholesale_is_picked_up()
    {
        var settings = new AppSettings { EnableEmojiShuffle = true };
        settings.EmojiCollection.Add("old");

        var service = new EmojiService(settings);
        Assert.Equal(new[] { "old" }, service.EmojiSnapshot);

        settings.EmojiCollection = new System.Collections.ObjectModel.ObservableCollection<string> { "new" };

        Assert.Equal(new[] { "new" }, service.EmojiSnapshot);

        settings.EmojiCollection.Add("newer");
        Assert.Equal(new[] { "new", "newer" }, service.EmojiSnapshot);
    }
}
