// Licensed to b2soft under the MIT license

using System;
using System.Threading.Tasks;

namespace LiveCity.Shared
{
	public static class TaskExtensions
	{
		public static void HandleError(this Task task)
		{
			if (!task.IsFaulted) return;
			Console.WriteLine($"Task failed with exception: {task.Exception}");
		}
	}
}
