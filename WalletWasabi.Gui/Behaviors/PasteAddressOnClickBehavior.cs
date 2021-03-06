using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NBitcoin.Payment;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Gui.Controls;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Gui.Behaviors
{
	public class PasteAddressOnClickBehavior : CommandBasedBehavior<TextBox>
	{
		private TextBoxState _textBoxState = TextBoxState.None;

		public PasteAddressOnClickBehavior()
		{
			Global = Locator.Current.GetService<Global>();
		}

		protected internal enum TextBoxState
		{
			None,
			NormalTextBoxOperation,
			AddressInsert,
			SelectAll
		}

		private CompositeDisposable Disposables { get; set; }
		private Global Global { get; }

		private TextBoxState MyTextBoxState
		{
			get => _textBoxState;
			set
			{
				_textBoxState = value;
				switch (value)
				{
					case TextBoxState.NormalTextBoxOperation:
						{
							AssociatedObject.Cursor = new Cursor(StandardCursorType.Ibeam);
						}
						break;

					case TextBoxState.AddressInsert:
						{
							AssociatedObject.Cursor = new Cursor(StandardCursorType.Arrow);
						}
						break;

					case TextBoxState.SelectAll:
						{
							AssociatedObject.Cursor = new Cursor(StandardCursorType.Arrow);
						}
						break;
				}
			}
		}

		private bool ProcessText(string text)
		{
			if (AddressStringParser.TryParse(text, Global.Network, out BitcoinUrlBuilder? result))
			{
				AssociatedObject.Text = result.Address?.ToString();
				CommandParameter = result;
				ExecuteCommand();
				return true;
			}

			return false;
		}

		protected override void OnAttached()
		{
			Disposables?.Dispose();

			Disposables = new CompositeDisposable
			{
				AssociatedObject.GetObservable(InputElement.IsFocusedProperty).Subscribe(focused =>
					{
						if (!focused)
						{
							MyTextBoxState = TextBoxState.None;
						}
					}),
				AssociatedObject
					.GetObservable(InputElement.KeyUpEvent)
					.Throttle(TimeSpan.FromMilliseconds(500)) // Do not remove this we need to make sure we are running on a separate Task.
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(_ =>
					{
						ProcessText(AssociatedObject.Text);
						MyTextBoxState = TextBoxState.NormalTextBoxOperation;
					})
			};

			if (AssociatedObject is ExtendedTextBox extendedTextBox)
			{
				Disposables.Add(extendedTextBox.TextPasted
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(text =>
					{
						ProcessText(text);
						MyTextBoxState = TextBoxState.NormalTextBoxOperation;
					}));
			}

			Disposables.Add(
				AssociatedObject.GetObservable(InputElement.PointerReleasedEvent).Subscribe(async pointer =>
					{
						if (!Global.UiConfig.Autocopy)
						{
							return;
						}

						switch (MyTextBoxState)
						{
							case TextBoxState.AddressInsert:
								{
									string text = await Application.Current.Clipboard.GetTextAsync();
									ProcessText(text);
									MyTextBoxState = TextBoxState.NormalTextBoxOperation;
								}
								break;

							case TextBoxState.SelectAll:
								{
									AssociatedObject.SelectionStart = 0;
									AssociatedObject.SelectionEnd = AssociatedObject.Text.Length;
									MyTextBoxState = TextBoxState.NormalTextBoxOperation;
								}
								break;
						}
					}));

			Disposables.Add(
				AssociatedObject.GetObservable(InputElement.PointerEnterEvent).Subscribe(async pointerEnter =>
					{
						if (!Global.UiConfig.Autocopy)
						{
							return;
						}

						if (!AssociatedObject.IsFocused && MyTextBoxState == TextBoxState.NormalTextBoxOperation)
						{
							MyTextBoxState = TextBoxState.None;
						}

						if (MyTextBoxState == TextBoxState.NormalTextBoxOperation)
						{
							return;
						}

						if (string.IsNullOrEmpty(AssociatedObject.Text))
						{
							string text = await Application.Current.Clipboard.GetTextAsync();
							MyTextBoxState = AddressStringParser.TryParse(text, Global.Network, out _)
								? TextBoxState.AddressInsert
								: TextBoxState.NormalTextBoxOperation;
						}
						else
						{
							MyTextBoxState = TextBoxState.SelectAll;
						}
					}));

			base.OnAttached();
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
