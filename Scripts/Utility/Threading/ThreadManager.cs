﻿using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Collections;

namespace Rynchodon.Threading
{
	public class ThreadManager
	{

		private const int QueueOverflow = 1000000;

		private readonly Logger myLogger = new Logger("ThreadManager");

		private readonly bool Background;
		private readonly string ThreadName;

		private readonly FastResourceLock lock_parallelTasks = new FastResourceLock();

		private MyQueue<Action> ActionQueue = new MyQueue<Action>(128);
		private readonly FastResourceLock lock_ActionQueue = new FastResourceLock();

		public readonly byte AllowedParallel;

		public byte ParallelTasks { get; private set; }

		public ThreadManager(byte AllowedParallel = 1, bool background = false, string threadName = null)
		{
			this.myLogger = new Logger("ThreadManager", () => threadName ?? string.Empty, () => ParallelTasks.ToString());
			this.AllowedParallel = AllowedParallel;
			this.Background = background;
			this.ThreadName = threadName;
			MyAPIGateway.Entities.OnCloseAll += Entities_OnCloseAll;
		}

		private void Entities_OnCloseAll()
		{
			MyAPIGateway.Entities.OnCloseAll -= Entities_OnCloseAll;
			myLogger.debugLog("stopping thread", "Entities_OnCloseAll()", Logger.severity.INFO);
			ActionQueue = null;
		}

		public void EnqueueAction(Action toQueue)
		{
			using (lock_ActionQueue.AcquireExclusiveUsing())
			{
				ActionQueue.Enqueue(toQueue);
				VRage.Exceptions.ThrowIf<Exception>(ActionQueue.Count > QueueOverflow, "queue is too long");
			}

			using (lock_parallelTasks.AcquireExclusiveUsing())
			{
				if (ParallelTasks >= AllowedParallel)
					return;
				ParallelTasks++;
			}

			MyAPIGateway.Utilities.InvokeOnGameThread(() => {
				if (Background)
					MyAPIGateway.Parallel.StartBackground(Run);
				else
					MyAPIGateway.Parallel.Start(Run);
			});
		}

		public void EnqueueIfIdle(Action toQueue)
		{
			bool idle;
			using (lock_ActionQueue.AcquireSharedUsing())
				idle = ActionQueue.Count == 0;

			if (idle)
				EnqueueAction(toQueue);
		}

		private void Run()
		{
			try
			{
				if (ThreadName != null)
					ThreadTracker.ThreadName = ThreadName + '(' + ThreadTracker.ThreadNumber + ')';
				Action currentItem;
				while (true)
				{
					using (lock_ActionQueue.AcquireExclusiveUsing())
					{
						if (ActionQueue.Count == 0)
							return;
						currentItem = ActionQueue.Dequeue();
					}
					if (currentItem != null)
						currentItem();
				}
			}
			catch (Exception ex) { myLogger.alwaysLog("Exception: " + ex, "Run()", Logger.severity.ERROR); }
			finally
			{
				using (lock_parallelTasks.AcquireExclusiveUsing())
					ParallelTasks--;
				ThreadTracker.ThreadName = null;
			}
		}

	}
}
