using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using Newtonsoft.Json;

namespace Microsoft.BotBuilderSamples
{
    public class DispatchBot : ActivityHandler
    {
        private readonly ILogger<DispatchBot> _logger;
        private readonly IBotServices _botServices;

        public DispatchBot(IBotServices botServices, BookingDialog bookingDialog, ILogger<DispatchBot> logger)
        {
            _logger = logger;
            _botServices = botServices;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var recognizerResult = await _botServices.Dispatch.RecognizeAsync(turnContext, cancellationToken);
            
            var topIntent = recognizerResult.GetTopScoringIntent();
            
            await DispatchToTopIntentAsync(turnContext, topIntent.intent, recognizerResult, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            const string WelcomeText = "請試著輸入一些問題";

            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var welcomeCard = CreateAdaptiveCardAttachment();
                    var response = MessageFactory.Attachment(welcomeCard, ssml: "Welcome to Bot Framework!");
                    await turnContext.SendActivityAsync(response, cancellationToken);
                    await turnContext.SendActivityAsync(MessageFactory.Text($"歡迎來到測試Bot. {WelcomeText}"), cancellationToken);
                }
            }
        }

        private async Task DispatchToTopIntentAsync(ITurnContext<IMessageActivity> turnContext, string intent, RecognizerResult recognizerResult, CancellationToken cancellationToken)
        {
            switch (intent)
            {
                case "l_HomeAutomation":
                    await ProcessHomeAutomationAsync(turnContext, recognizerResult.Properties["luisResult"] as LuisResult, cancellationToken);
                    break;
                case "l_Weather":
                    await ProcessWeatherAsync(turnContext, recognizerResult.Properties["luisResult"] as LuisResult, cancellationToken);
                    break;
                case "l_Flight":
                    await ProcessWeatherAsync(turnContext, recognizerResult.Properties["luisResult"] as LuisResult, cancellationToken);
                    break;
                case "q_sample-qna":
                    await ProcessSampleQnAAsync(turnContext, cancellationToken);
                    break;
                default:
                    _logger.LogInformation($"Dispatch unrecognized intent: {intent}.");
                    await turnContext.SendActivityAsync(MessageFactory.Text($"抱歉，不懂這個問題的意思"), cancellationToken);
                    break;
            }
        }

        private async Task ProcessHomeAutomationAsync(ITurnContext<IMessageActivity> turnContext, LuisResult luisResult, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessHomeAutomationAsync");

            var result = luisResult.ConnectedServiceResult;
            var topIntent = result.TopScoringIntent.Intent; 
            
            await turnContext.SendActivityAsync(MessageFactory.Text($"HomeAutomation top intent {topIntent}."), cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Text($"HomeAutomation intents detected:\n\n{string.Join("\n\n", result.Intents.Select(i => i.Intent))}"), cancellationToken);
            if (luisResult.Entities.Count > 0)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"HomeAutomation entities were found in the message:\n\n{string.Join("\n\n", result.Entities.Select(i => i.Entity))}"), cancellationToken);
            }
        }

        private async Task ProcessFlightAsync(ITurnContext<IMessageActivity> turnContext, LuisResult luisResult, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessFlightAsync");

            var result = luisResult.ConnectedServiceResult;
            var topIntent = result.TopScoringIntent.Intent;

            await turnContext.SendActivityAsync(MessageFactory.Text($"Flight top intent {topIntent}."), cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Text($"Flight intents detected:\n\n{string.Join("\n\n", result.Intents.Select(i => i.Intent))}"), cancellationToken);
            if (luisResult.Entities.Count > 0)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Flight entities were found in the message:\n\n{string.Join("\n\n", result.Entities.Select(i => i.Entity))}"), cancellationToken);
            }
        }

        private async Task ProcessWeatherAsync(ITurnContext<IMessageActivity> turnContext, LuisResult luisResult, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessWeatherAsync");

            var result = luisResult.ConnectedServiceResult;
            var topIntent = result.TopScoringIntent.Intent;
            await turnContext.SendActivityAsync(MessageFactory.Text($"ProcessWeather top intent {topIntent}."), cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Text($"ProcessWeather Intents detected::\n\n{string.Join("\n\n", result.Intents.Select(i => i.Intent))}"), cancellationToken);
            if (luisResult.Entities.Count > 0)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"ProcessWeather entities were found in the message:\n\n{string.Join("\n\n", result.Entities.Select(i => i.Entity))}"), cancellationToken);
            }
        }

        private async Task ProcessSampleQnAAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessSampleQnAAsync");

            var results = await _botServices.SampleQnA.GetAnswersAsync(turnContext);
            if (results.Any())
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(results.First().Answer), cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, could not find an answer in the Q and A system."), cancellationToken);
            }
        }
        private Attachment CreateAdaptiveCardAttachment()
        {
            var cardResourcePath = "Bot.Cards.welcomeCard.json";

            using (var stream = GetType().Assembly.GetManifestResourceStream(cardResourcePath))
            {
                using (var reader = new StreamReader(stream))
                {
                    var adaptiveCard = reader.ReadToEnd();
                    return new Attachment()
                    {
                        ContentType = "application/vnd.microsoft.card.adaptive",
                        Content = JsonConvert.DeserializeObject(adaptiveCard),
                    };
                }
            }
        }
        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Result is BookingDetails result)
            {

                var timeProperty = new TimexProperty(result.TravelDate);
                var travelDateMsg = timeProperty.ToNaturalLanguage(DateTime.Now);
                var messageText = $"已幫你訂機票從{result.Origin}到{result.Destination}在 {result.TravelDate}";
                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                await stepContext.Context.SendActivityAsync(message, cancellationToken);
            }

            var promptMessage = "有其他可以幫忙的嗎?";
            return await stepContext.ReplaceDialogAsync(_botServices.component.InitialDialogId, promptMessage, cancellationToken);
        }
    }
}
