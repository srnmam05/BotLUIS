using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly FlightBookingRecognizer _luisRecognizers;
        private readonly ILogger<MainDialog> _logger;
        private readonly BotServices _luisRecognizer;
        private readonly IBotServices _botServices;

        public MainDialog(BotServices luisRecognizer, BookingDialog bookingDialog, ILogger<MainDialog> logger, IBotServices botServices, FlightBookingRecognizer luisRecognizers)
            : base(nameof(MainDialog))
        {
            _luisRecognizer = luisRecognizer;
            _logger = logger;
            _botServices = botServices;
            _luisRecognizers = luisRecognizers;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(bookingDialog);
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {

            var messageText = stepContext.Options?.ToString() ?? "今天可以幫忙什麼?\n 試著說什麼或問問題";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {

            var luisResult = await _luisRecognizers.RecognizeAsync<FlightBooking>(stepContext.Context, cancellationToken);
            var recognizerResult = await _botServices.Dispatch.RecognizeAsync(stepContext.Context, cancellationToken);
            var topIntent = recognizerResult.GetTopScoringIntent();
            switch (topIntent.intent)
            {
                case "l_Flight":
                    await ShowWarningForUnsupportedCities(stepContext.Context, luisResult, cancellationToken);

                    var bookingDetails = new BookingDetails()
                    {
                        Destination = luisResult.ToEntities.Airport,
                        Origin = luisResult.FromEntities.Airport,
                        TravelDate = luisResult.TravelDate,
                    };

                    return await stepContext.BeginDialogAsync(nameof(BookingDialog), bookingDetails, cancellationToken);

                default:
                    await DispatchToTopIntentAsync(stepContext.Context, topIntent.intent, recognizerResult, cancellationToken);
                    break;
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        private static async Task ShowWarningForUnsupportedCities(ITurnContext context, FlightBooking luisResult, CancellationToken cancellationToken)
        {
            var unsupportedCities = new List<string>();

            var fromEntities = luisResult.FromEntities;
            if (!string.IsNullOrEmpty(fromEntities.From) && string.IsNullOrEmpty(fromEntities.Airport))
            {
                unsupportedCities.Add(fromEntities.From);
            }

            var toEntities = luisResult.ToEntities;
            if (!string.IsNullOrEmpty(toEntities.To) && string.IsNullOrEmpty(toEntities.Airport))
            {
                unsupportedCities.Add(toEntities.To);
            }

            if (unsupportedCities.Any())
            {
                var messageText = $"Sorry but the following airports are not supported: {string.Join(',', unsupportedCities)}";
                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                await context.SendActivityAsync(message, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {

            if (stepContext.Result is BookingDetails result)
            {

                var timeProperty = new TimexProperty(result.TravelDate);
                var travelDateMsg = timeProperty.ToNaturalLanguage(DateTime.Now);
                var messageText = $"已幫你訂從{result.Origin}到{result.Destination}的機票時間是 {result.TravelDate}";
                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                await stepContext.Context.SendActivityAsync(message, cancellationToken);
            }

            var promptMessage = "有其他可以幫忙的嗎?";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        }
        private async Task DispatchToTopIntentAsync(ITurnContext turnContext, string intent, RecognizerResult recognizerResult, CancellationToken cancellationToken)
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

        private async Task ProcessHomeAutomationAsync(ITurnContext turnContext, LuisResult luisResult, CancellationToken cancellationToken)
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

        private async Task ProcessFlightAsync(ITurnContext turnContext, LuisResult luisResult, CancellationToken cancellationToken)
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

        private async Task ProcessWeatherAsync(ITurnContext turnContext, LuisResult luisResult, CancellationToken cancellationToken)
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

        private async Task ProcessSampleQnAAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProcessSampleQnAAsync");

            var results = await _botServices.SampleQnA.GetAnswersAsync(turnContext);
            if (results.Any())
            {
                if (turnContext.Activity.Text.ToString()=="ryuk"|| turnContext.Activity.Text.ToString() =="豪哥")
                {
                    await turnContext.SendActivityAsync(MessageFactory.ContentUrl("https://i.imgur.com/2V5ScbX.jpg", "image/jpg"));
                }
                else if(turnContext.Activity.Text.ToString()=="爛")
                {
                    await turnContext.SendActivityAsync(MessageFactory.ContentUrl("https://i.imgur.com/wwkjcC4.jpg", "image/jpg"));
                    await turnContext.SendActivityAsync(MessageFactory.ContentUrl("https://i.imgur.com/HqQegnt.jpg", "image/jpg"));
                    await turnContext.SendActivityAsync(MessageFactory.ContentUrl("https://i.imgur.com/xY3RtxX.jpg", "image/jpg"));
                }
                else if (results.First().Answer.Contains("mp4"))
                {
                    await turnContext.SendActivityAsync(MessageFactory.ContentUrl("http://i.imgur.com/ZNIBbnV.gif", "image/gif"));
                }
                else
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(results.First().Answer), cancellationToken);
                }
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("抱歉，資料庫找不到答案"), cancellationToken);
            }
        }
    }
}
