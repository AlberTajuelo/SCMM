﻿using System;
using System.Linq;

namespace SCMM.Steam.Shared
{
    public static class SteamEconomyHelper
	{
		public const decimal SteamFeeMultiplier = 0.8695652173928261m; // 13%
		public const decimal SteamFeePlatformMultiplier = 0.0304347811170578m; // 3%
		public const decimal SteamFeePublisherMultiplier = 0.100000001490116119m; // 10%

		public static long GetSteamFeePlatformComponentAsInt(long value)
		{
			// Minimum platform fee is 0.01 units
			return (long) Math.Floor(Math.Max((decimal) value * SteamFeePlatformMultiplier, 1));
		}

		public static long GetSteamFeePublisherComponentAsInt(long value)
		{
			// Minimum publisher fee is 0.01 units
			return (long) Math.Floor(Math.Max((decimal) value * SteamFeePublisherMultiplier, 1));
		}

		public static long GetSteamFeeAsInt(long value)
		{
			// Add both fees together to ensure the minimum fee component of 0.02
			return (GetSteamFeePlatformComponentAsInt(value) + GetSteamFeePublisherComponentAsInt(value));
		}

		/// <summary>
		/// C# port of the Steam economy common logic
		/// https://steamcommunity-a.akamaihd.net/public/javascript/economy_common.js?v=tsXdRVB0yEaR&l=english
		/// </summary>
		public static int GetQuantityValueAsInt(string strAmount)
        {
			if (String.IsNullOrEmpty(strAmount))
			{
				return 0;
			}

			strAmount = new string(strAmount.Where(c => char.IsDigit(c)).ToArray());
			return int.Parse(strAmount);
		}

		/// <summary>
		/// C# port of the Steam economy common logic
		/// https://steamcommunity-a.akamaihd.net/public/javascript/economy_common.js?v=tsXdRVB0yEaR&l=english
		/// </summary>
		public static long GetPriceValueAsInt(string strAmount)
		{
			long nAmount = 0;
			if (String.IsNullOrEmpty(strAmount))
			{
				return 0;
			}

			// Custom work around for strings that have more than 2 decimal places, round down
			var decAmount = 0m;
			if (decimal.TryParse(strAmount, out decAmount))
            {
				decAmount = Math.Round(decAmount, 2);
				strAmount = decAmount.ToString();
            }

			// Users may enter either comma or period for the decimal mark and digit group separators.
			strAmount = strAmount.Replace(',', '.');

			// strip the currency symbol, set .-- to .00
			strAmount = strAmount.Replace(".--", ".00");
			strAmount = new string(strAmount.Where(c => char.IsDigit(c) || c == '.').ToArray());

			// strip spaces
			strAmount = strAmount.Replace(" ", String.Empty);

			// Remove all but the last period so that entries like "1,147.6" work
			if (strAmount.IndexOf('.') != -1)
			{
				var splitAmount = strAmount.Split('.');
				var strLastSegment = splitAmount.Length > 0 ? splitAmount[splitAmount.Length - 1] : null;

				if (!String.IsNullOrEmpty(strLastSegment) && strLastSegment.Length == 3 && Int64.Parse(splitAmount[splitAmount.Length - 2]) != 0)
				{
					// Looks like the user only entered thousands separators. Remove all commas and periods.
					// Ensures an entry like "1,147" is not treated as "1.147"
					//
					// Users may be surprised to find that "1.147" is treated as "1,147". "1.147" is either an error or the user
					// really did mean one thousand one hundred and forty seven since no currencies can be split into more than
					// hundredths. If it was an error, the user should notice in the next step of the dialog and can go back and
					// correct it. If they happen to not notice, it is better that we list the item at a higher price than
					// intended instead of lower than intended (which we would have done if we accepted the 1.147 value as is).
					strAmount = String.Join(String.Empty, splitAmount);
				}
				else
				{
					strAmount = String.Join(String.Empty, splitAmount.Take(splitAmount.Length - 1)) + '.' + strLastSegment;
				}
			}

			var flAmount = decimal.Parse(strAmount) * 100;
			nAmount = (long) Math.Floor(flAmount + 0.000001m); // round down

			nAmount = Math.Max(nAmount, 0);
			return nAmount;
		}

		/*
		/// <summary>
		/// C# port of the Steam economy common logic
		/// https://steamcommunity-a.akamaihd.net/public/javascript/economy_common.js?v=tsXdRVB0yEaR&l=english
		/// </summary>
		public static long CalculateFeeAmount(long amount, long publisherFee)
		{
			if (!g_rgWalletInfo['wallet_fee'])
				return 0;

			publisherFee = (typeof publisherFee == 'undefined') ? 0 : publisherFee;

			// Since CalculateFeeAmount has a Math.floor, we could be off a cent or two. Let's check:
			var iterations = 0; // shouldn't be needed, but included to be sure nothing unforseen causes us to get stuck
			var nEstimatedAmountOfWalletFundsReceivedByOtherParty = parseInt((amount - parseInt(g_rgWalletInfo['wallet_fee_base'])) / (parseFloat(g_rgWalletInfo['wallet_fee_percent']) + parseFloat(publisherFee) + 1));

			var bEverUndershot = false;
			var fees = CalculateAmountToSendForDesiredReceivedAmount(nEstimatedAmountOfWalletFundsReceivedByOtherParty, publisherFee);
			while (fees.amount != amount && iterations < 10)
			{
				if (fees.amount > amount)
				{
					if (bEverUndershot)
					{
						fees = CalculateAmountToSendForDesiredReceivedAmount(nEstimatedAmountOfWalletFundsReceivedByOtherParty - 1, publisherFee);
						fees.steam_fee += (amount - fees.amount);
						fees.fees += (amount - fees.amount);
						fees.amount = amount;
						break;
					}
					else
					{
						nEstimatedAmountOfWalletFundsReceivedByOtherParty--;
					}
				}
				else
				{
					bEverUndershot = true;
					nEstimatedAmountOfWalletFundsReceivedByOtherParty++;
				}

				fees = CalculateAmountToSendForDesiredReceivedAmount(nEstimatedAmountOfWalletFundsReceivedByOtherParty, publisherFee);
				iterations++;
			}

			// fees.amount should equal the passed in amount

			return fees;
		}
		
		/// <summary>
		/// C# port of the Steam economy common logic
		/// https://steamcommunity-a.akamaihd.net/public/javascript/economy_common.js?v=tsXdRVB0yEaR&l=english
		/// </summary>
		public static long CalculateAmountToSendForDesiredReceivedAmount(long receivedAmount, long publisherFee )
		{
			if (!g_rgWalletInfo['wallet_fee'])
			{
				return receivedAmount;
			}

			publisherFee = (typeof publisherFee == 'undefined') ? 0 : publisherFee;

			var nSteamFee = parseInt(Math.floor(Math.max(receivedAmount * parseFloat(g_rgWalletInfo['wallet_fee_percent']), g_rgWalletInfo['wallet_fee_minimum']) + parseInt(g_rgWalletInfo['wallet_fee_base'])));
			var nPublisherFee = parseInt(Math.floor(publisherFee > 0 ? Math.max(receivedAmount * publisherFee, 1) : 0));
			var nAmountToSend = receivedAmount + nSteamFee + nPublisherFee;

			return {
			steam_fee: nSteamFee,
		publisher_fee: nPublisherFee,
		fees: nSteamFee + nPublisherFee,
		amount: parseInt(nAmountToSend)
			};
		}
		*/
	}
}
