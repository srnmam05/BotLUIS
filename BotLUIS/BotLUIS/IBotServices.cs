using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Builder.Dialogs;

namespace Microsoft.BotBuilderSamples
{
    public interface IBotServices :IRecognizer
    {
        LuisRecognizer Dispatch { get; }
        QnAMaker SampleQnA { get; }
        ComponentDialog component { get; set; }
        ActivityHandler handler { get; set; }
    }
}
