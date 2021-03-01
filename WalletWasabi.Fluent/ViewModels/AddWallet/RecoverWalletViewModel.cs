using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData (Title = "Enter recovery words")]
	public partial class RecoverWalletViewModel : RoutableViewModel
	{
		[AutoNotify] private IEnumerable<string>? _suggestions;
		[AutoNotify] private Mnemonic? _currentMnemonics;

		public RecoverWalletViewModel(
			string walletName,
			WalletManagerViewModel walletManagerViewModel)
		{
			Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();
			var walletManager = walletManagerViewModel.WalletManager;
			var network = walletManager.Network;

			Mnemonics.ToObservableChangeSet().ToCollection()
				.Select(x => x.Count == 12 ? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant()) : default)
				.Subscribe(x => CurrentMnemonics = x);

			this.WhenAnyValue(x => x.CurrentMnemonics)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(Mnemonics)));

			this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

			FinishCommandCanExecute =
				this.WhenAnyValue(x => x.CurrentMnemonics)
					.Select(currentMnemonics => currentMnemonics is { } && !Validations.Any);

			NextCommand = ReactiveCommand.CreateFromTask(
				async () => await OnNext(walletManager, network, walletName),
				FinishCommandCanExecute);

			AdvancedOptionsInteraction = new Interaction<(KeyPath, int), (KeyPath?, int?)>();
			AdvancedOptionsInteraction.RegisterHandler(
				async interaction =>
					interaction.SetOutput(
						(await new AdvancedRecoveryOptionsViewModel(interaction.Input).ShowDialogAsync()).Result));

			AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var (accountKeyPathIn, minGapLimitIn) = await AdvancedOptionsInteraction
						.Handle((AccountKeyPath, MinGapLimit)).ToTask();

					if (accountKeyPathIn is { } && minGapLimitIn is { })
					{
						AccountKeyPath = accountKeyPathIn;
						MinGapLimit = (int)minGapLimitIn;
					}
				});

			EnableAutoBusyOn(NextCommand);
		}

		private async Task OnNext(
			WalletManager walletManager,
			Network network,
			string? walletName)
		{
			var dialogResult = await NavigateDialog(
				new CreatePasswordDialogViewModel(
					"Type the password of the wallet to be able to recover and click Continue."));

			if (dialogResult.Result is { } password)
			{
				try
				{
					var keyManager = await Task.Run(
						() =>
						{
							var walletFilePath = walletManager.WalletDirectories.GetWalletFilePaths(walletName!)
								.walletFilePath;

							var result = KeyManager.Recover(
								CurrentMnemonics!,
								password!,
								walletFilePath,
								AccountKeyPath,
								MinGapLimit);

							result.SetNetwork(network);

							return result;
						});

					Navigate().To(new AddedWalletPageViewModel(walletManager, keyManager));

					return;
				}
				catch (Exception ex)
				{
					// TODO navigate to error dialog.
					Logger.LogError(ex);
				}
			}

			if (dialogResult.Kind == DialogResultKind.Cancel)
			{
				Navigate().Clear();
			}
		}

		public IObservable<bool> FinishCommandCanExecute { get; }

		public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

		private KeyPath AccountKeyPath { get; set; } = KeyPath.Parse("m/84'/0'/0'");

		private int MinGapLimit { get; set; } = 63;

		private Interaction<(KeyPath, int), (KeyPath?, int?)> AdvancedOptionsInteraction { get; }

		public ObservableCollection<string> Mnemonics { get; } = new();

		private void ValidateMnemonics(IValidationErrors errors)
		{
			if (CurrentMnemonics is { } && !CurrentMnemonics.IsValidChecksum)
			{
				errors.Add(ErrorSeverity.Error, "Recovery words are not valid.");
			}
		}

		private string GetTagsAsConcatString()
		{
			return string.Join(' ', Mnemonics);
		}
	}
}
