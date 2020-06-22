using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class DateResolverDialog : CancelAndHelpDialog
    {
        private const string PromptMsgText = "什麼時間要去?";
        private const string RepromptMsgText = "抱歉，請輸入日期.";

        public DateResolverDialog(string id = null)
            : base(id ?? nameof(DateResolverDialog))
        {
            AddDialog(new DateTimePrompt(nameof(DateTimePrompt), DateTimePromptValidator));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                InitialStepAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var timex = (string)stepContext.Options;

            var promptMessage = MessageFactory.Text(PromptMsgText, PromptMsgText, InputHints.ExpectingInput);
            var repromptMessage = MessageFactory.Text(RepromptMsgText, RepromptMsgText, InputHints.ExpectingInput);

            if (timex == null)
            {

                return await stepContext.PromptAsync(nameof(DateTimePrompt),
                    new PromptOptions
                    {
                        Prompt = promptMessage,
                        RetryPrompt = repromptMessage,
                    }, cancellationToken);
            }

            var timexProperty = new TimexProperty(timex);
            if (!timexProperty.Types.Contains(Constants.TimexTypes.Definite))
            {
                return await stepContext.PromptAsync(nameof(DateTimePrompt),
                    new PromptOptions
                    {
                        Prompt = repromptMessage,
                    }, cancellationToken);
            }

            return await stepContext.NextAsync(new List<DateTimeResolution> { new DateTimeResolution { Timex = timex } }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var timex = ((List<DateTimeResolution>)stepContext.Result)[0].Timex;
            return await stepContext.EndDialogAsync(timex, cancellationToken);
        }

        private static Task<bool> DateTimePromptValidator(PromptValidatorContext<IList<DateTimeResolution>> promptContext, CancellationToken cancellationToken)
        {
            if (promptContext.Recognized.Succeeded)
            {

                var timex = promptContext.Recognized.Value[0].Timex.Split('T')[0];

                var isDefinite = new TimexProperty(timex).Types.Contains(Constants.TimexTypes.Definite);

                return Task.FromResult(isDefinite);
            }

            return Task.FromResult(false);
        }
    }
}
