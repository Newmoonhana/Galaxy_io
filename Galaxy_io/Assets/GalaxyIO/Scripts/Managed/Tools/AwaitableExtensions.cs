using System;
using UnityEngine;

namespace GalaxyIO.Tools
{
    /// <summary>
    /// <see cref="Awaitable"/> 확장 메서드.
    /// </summary>
    public static class AwaitableExtensions
    {
        /// <summary>
        /// 기다리지 않고 실행함. 예외는 콘솔에 남기고, 취소는 무시함.
        /// <c>_ = SomeAsync()</c> 는 예외를 조용히 삼키므로 대신 사용할 것.
        /// </summary>
        public static async void Forget(this Awaitable _awaitable)
        {
            try
            {
                await _awaitable;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
