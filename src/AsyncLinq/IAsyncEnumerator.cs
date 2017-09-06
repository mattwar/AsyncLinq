// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsyncLinq
{
    public interface IAsyncEnumerator<out T> : IDisposable
    {
        Task<bool> MoveNextAsync();
        T TryGetNext(out bool success);
    }
}
