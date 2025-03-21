using System;
using System.Threading.Tasks;

namespace Wsla
{
    public static class SharedExtensions
    {
        public static async void Forget(this Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                NetworkLog.Error(ex);
            }
        }
        public static async void Forget<T>(this Task<T> task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                NetworkLog.Error(ex);
            }
        }

        public static async void Forget(this ValueTask task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                NetworkLog.Error(ex);
            }
        }
        public static async void Forget<T>(this ValueTask<T> task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                NetworkLog.Error(ex);
            }
        }
    }
}