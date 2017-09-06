﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

namespace AsyncLinq
{
    public interface IAsyncEnumerable<T>
    {
        IAsyncEnumerator<T> GetEnumerator();
    }
}
