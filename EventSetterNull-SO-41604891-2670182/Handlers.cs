﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace EventSetterNull_SO_41604891_2670182
{
	public static class Handlers
	{
		public static RoutedEventHandler StyleClick
		{
			get
			{
				return styleClick;
			}
		}

		private static void styleClick (object sender, RoutedEventArgs e)
		{
			Debug.WriteLine("StyleClick");
		}
	}
}