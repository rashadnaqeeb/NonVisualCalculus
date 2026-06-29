using System.Collections.Generic;
using DiscoAccess.Core.Speech;
using Xunit;

namespace DiscoAccess.Tests
{
    public class SpeechPipelineTests
    {
        private sealed class FakeBackend : ISpeechBackend
        {
            public readonly List<string> Spoken = new List<string>();
            public bool IsAvailable => true;
            public void Speak(string text, bool interrupt) => Spoken.Add(text);
            public void Stop() { }
        }

        public SpeechPipelineTests()
        {
            // The tap is a static seam shared across tests; reset it so cases don't leak into each other.
            SpeechPipeline.Spoken = null;
        }

        [Fact]
        public void Speak_InvokesTap_AfterCleanGate()
        {
            var pipeline = new SpeechPipeline(new FakeBackend());
            var tapped = new List<string>();
            SpeechPipeline.Spoken = (text, interrupt) => tapped.Add(text);

            // Rich-text markup is cleaned before the tap sees it.
            pipeline.Speak("<b>Detective</b>", interrupt: true);

            Assert.Equal(new[] { "Detective" }, tapped);
        }

        [Fact]
        public void Speak_InvokesTap_ForEachRepeatedLine()
        {
            var pipeline = new SpeechPipeline(new FakeBackend());
            var tapped = new List<string>();
            SpeechPipeline.Spoken = (text, interrupt) => tapped.Add(text);

            pipeline.Speak("minimum");
            pipeline.Speak("minimum");

            Assert.Equal(new[] { "minimum", "minimum" }, tapped);
        }
    }
}
