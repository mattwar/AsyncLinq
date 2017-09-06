using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AsyncLinq;

namespace UnitTests
{
    public static class AsyncTextExtensions
    {
        public static IAsyncEnumerable<string> GetAsyncLines(this TextReader reader)
        {
            return new AsyncLineReader(reader);
        }

        public static IAsyncEnumerable<string> GetAsyncLines(this string text)
        {
            return GetAsyncLines(new StringReader(text));
        }

        private class AsyncLineReader : IAsyncEnumerable<string>
        {
            private readonly TextReader reader;
            private readonly bool includeEOL;

            public AsyncLineReader(TextReader reader, bool includeEOL = false)
            {
                this.reader = reader;
                this.includeEOL = includeEOL;
            }

            public IAsyncEnumerator<string> GetEnumerator()
            {
                return new AsyncLineEnumerator(this.reader, this.includeEOL);
            }

            private class AsyncLineEnumerator : IAsyncEnumerator<string>
            {
                private readonly TextReader reader;
                private readonly bool includeEOL;
                private readonly StringBuilder builder;
                private readonly char[] buffer;
                private int bufferCount;
                private int bufferOffset;
                private bool atEnd;

                public AsyncLineEnumerator(TextReader reader, bool includeEOL)
                {
                    this.reader = reader;
                    this.includeEOL = includeEOL;
                    this.builder = new StringBuilder();
                    this.buffer = new char[1024];
                }

                public void Dispose()
                {
                    this.reader.Dispose();
                }

                public async Task<bool> MoveNextAsync()
                {
                    while (true)
                    {
                        if (this.bufferOffset < this.bufferCount)
                        {
                            if (this.GetStartOfNextLine() > 0)
                            {
                                // the rest of a complete line is in the buffer.
                                // return true and complete the line on call to TryGetNext
                                return true;
                            }
                            else
                            {
                                // add remaining characters in buffer to builder
                                this.builder.Append(this.buffer, this.bufferOffset, this.bufferCount - this.bufferOffset);
                            }
                        }

                        this.bufferOffset = 0;
                        this.bufferCount = await this.reader.ReadBlockAsync(this.buffer, 0, this.buffer.Length).ConfigureAwait(false);

                        if (this.bufferCount == 0)
                        {
                            if (this.builder.Length > 0)
                            {
                                this.atEnd = true;
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }

                public string TryGetNext(out bool success)
                {
                    if (this.atEnd)
                    {
                        if (this.builder.Length > 0)
                        {
                            success = true;
                            return this.GetLine();
                        }
                        else
                        {
                            success = false;
                            return null;
                        }
                    }

                    if (this.bufferOffset < this.bufferCount)
                    {
                        var end = this.GetStartOfNextLine();
                        if (end > 0)
                        {
                            // copy the rest of the line into the builder.
                            this.builder.Append(this.buffer, this.bufferOffset, end - this.bufferOffset);
                            this.bufferOffset = end;
                            success = true;
                            return this.GetLine();
                        }
                    }

                    success = false;
                    return null;
                }

                private int GetStartOfNextLine()
                {
                    for (int i = this.bufferOffset; i < this.bufferCount; i++)
                    {
                        char c = this.buffer[i];
                        if (c == '\n')
                        {
                            return i + 1;
                        }
                    }

                    return -1;
                }

                private string GetLine()
                {
                    if (!this.includeEOL)
                    {
                        // remove EOL characters from end of line
                        if (this.builder.Length > 0 && this.builder[this.builder.Length - 1] == '\n')
                        {
                            this.builder.Length--;
                        }

                        if (this.builder.Length > 0 && this.builder[this.builder.Length - 1] == '\r')
                        {
                            this.builder.Length--;
                        }
                    }

                    var line = this.builder.ToString();
                    this.builder.Length = 0;
                    return line;
                }
            }
        }
    }
}