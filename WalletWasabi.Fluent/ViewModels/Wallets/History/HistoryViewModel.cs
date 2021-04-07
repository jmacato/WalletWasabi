using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.History
{
	public partial class HistoryViewModel
	{
		private readonly Wallet _wallet;
		private readonly BitcoinStore _bitcoinStore;

		[AutoNotify] private ObservableCollection<HistoryItemViewModel> _histories;
		[AutoNotify] private bool _hideCoinJoin;

		public HistoryViewModel(Wallet wallet, BitcoinStore bitcoinStore)
		{
			_wallet = wallet;
			_bitcoinStore = bitcoinStore;
			_histories = new ObservableCollection<HistoryItemViewModel>();

			this.WhenAnyValue(x => x.HideCoinJoin)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async _ => await UpdateHistoryAsync());
		}

		public async Task UpdateHistoryAsync()
		{
			try
			{
				var historyBuilder = new TransactionHistoryBuilder(_wallet);
				var txRecordList = await Task.Run(historyBuilder.BuildHistorySummary);

				Histories.Clear();
				var trs = txRecordList.Select(transactionSummary => new HistoryItemViewModel(transactionSummary, _bitcoinStore));

				if (HideCoinJoin)
				{
					trs = trs.Where(x => !x.IsCoinJoin);
				}

				Histories.AddRange(trs.Reverse());
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}
}
