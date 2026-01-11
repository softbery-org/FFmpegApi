// Version: 0.0.0.2
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FFmpegApi.Utilities.OverlayControl
{
    public class SequentialSaveQueue
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task Enqueue(Func<Task> saveAction)
        {
            await _semaphore.WaitAsync();
            try
            {
                await saveAction();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

}
