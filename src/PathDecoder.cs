
using System;
using System.Diagnostics;

namespace Pathing
{
    public class PathDecoder
    {
        // takes the raw target from the http request, extracts the path, and normalize it.
        // A) Un-escape %2F '/' and %2E '.'
        // B) Remove dot segments
        // Compare to https://github.com/dotnet/aspnetcore/blob/52de4ad8f1f635b3c97313d30eeb092567d6ad76/src/Servers/Kestrel/Core/src/Internal/Http/Http1Connection.cs#L259
        public static string GetPathFromRawTarget(string rawTarget)
        {
            if (rawTarget == null)
            {
                throw new ArgumentNullException(nameof(rawTarget));
            }

            if (rawTarget == "")
            {
                return rawTarget;
            }

            // OPTIONS *
            if (rawTarget == "*")
            {
                return string.Empty;
            }

            // Starts with '/' is Origin form https://tools.ietf.org/html/rfc7230#section-5.3.1
            if (rawTarget[0] == '/')
            {
                return GetPathFromOriginForm(rawTarget);
            }

            if (rawTarget.StartsWith("http://") || rawTarget.StartsWith("https://"))
            {
                return GetPathFromAbsoluteForm(rawTarget);
            }

            // The only thing left should be the Authority form which isn't supported.
            throw new NotSupportedException();
        }

        // https://datatracker.ietf.org/doc/html/rfc7230#section-5.3.2
        // "https://host:port/path?query
        // Uncommon
        private static string GetPathFromAbsoluteForm(string rawTarget)
        {
            var offset = rawTarget.IndexOf("://");
            Debug.Assert(offset > 0); // Already checked in the caller.
            offset += 3;
            var pathOffset = rawTarget.IndexOf("/", offset);
            var queryOffset = rawTarget.IndexOf("?", offset);
            if (pathOffset < 0)
            {
                // No path
                // http://host
                // http://host?query
                return "/";
            }
            // No query
            if (queryOffset < 0)
            {
                return GetPathFromOriginForm(rawTarget.Substring(pathOffset));
            }
            if (queryOffset < pathOffset)
            {
                // No path, slash in query
                // http://host?query/
                return "/";
            }

            // path and query
            // https://host:port/path?query
            return GetPathFromOriginForm(rawTarget.Substring(pathOffset, queryOffset - pathOffset));
        }

        // https://tools.ietf.org/html/rfc7230#section-5.3.1
        // "/path?query
        private static string GetPathFromOriginForm(string input)
        {
            // Extreamly common
            if (input == "/")
            {
                return input;
            }

            bool modified = false;

            var dotOffset = input.IndexOf('.');
            var percentOffset = input.IndexOf('%');
            var queryOffset = input.IndexOf('?');
            var doubleSlash = input.IndexOf("//");
            // Nothing to decode.
            if (dotOffset < 0 && percentOffset < 0 && queryOffset < 0 && doubleSlash < 0)
            {
                return input;
            }

            var path = new Span<char>(input.ToCharArray());

            // Remove the query
            if (queryOffset >= 0)
            {
                modified = true;
                path = path.Slice(0, queryOffset);
            }

            // Scan for %2F, %2E, un-escape them
            var newLength = DecodeInPlace(path);
            if (newLength < path.Length)
            {
                modified = true;
                path = path.Slice(0, newLength);
            }

            // Remove dot segments
            newLength = RemoveDotSegments(path);
            if (newLength < path.Length)
            {
                modified = true;
                path = path.Slice(0, newLength);
            }

            if (!modified)
            {
                return input;
            }

            return path.ToString();
        }

        // See https://github.com/dotnet/aspnetcore/blob/52de4ad8f1f635b3c97313d30eeb092567d6ad76/src/Shared/UrlDecoder/UrlDecoder.cs#L18
        public static int DecodeInPlace(Span<char> buffer)
        {
            // the slot to read the input
            var sourceIndex = 0;

            // the slot to write the unescaped byte
            var destinationIndex = 0;

            while (true)
            {
                if (sourceIndex == buffer.Length)
                {
                    break;
                }

                // We only care aboute two cases, %2E '.' and %2F '/', case insensitive.
                if (sourceIndex <= buffer.Length - 3 && buffer[sourceIndex] == '%'
                    && buffer[sourceIndex + 1] == '2')
                {
                    var hex = buffer[sourceIndex + 2];
                    if (hex == 'e' || hex == 'E')
                    {
                        buffer[destinationIndex++] = '.';
                        sourceIndex += 3;
                    }
                    else if (hex == 'f' || hex == 'F')
                    {
                        buffer[destinationIndex++] = '/';
                        sourceIndex += 3;
                    }
                    else
                    {
                        buffer[destinationIndex++] = buffer[sourceIndex++];
                    }
                }
                else
                {
                    buffer[destinationIndex++] = buffer[sourceIndex++];
                }
            }

            return destinationIndex;
        }

        // https://github.com/dotnet/aspnetcore/blob/8b30d862de6c9146f466061d51aa3f1414ee2337/src/Servers/Kestrel/Core/src/Internal/Http/PathNormalizer.cs#L54
        // Removes "/.", "/..", and empty segments "//"
        public static int RemoveDotSegments(Span<char> buffer)
        {
            if (!ContainsDotSegments(buffer))
            {
                return buffer.Length;
            }

            var src = 0;
            var dst = 0;

            while (src < buffer.Length)
            {
                var ch1 = buffer[src];
                Debug.Assert(ch1 == '/', "Path segment must always start with a '/'");

                char ch2, ch3, ch4;

                switch (buffer.Length - src)
                {
                    case 1:
                        break;
                    case 2:
                        ch2 = buffer[src + 1];

                        if (ch2 == '.' || ch2 == '/')
                        {
                            // B.  if the input buffer begins with a prefix of "/./" or "/.",
                            //     where "." is a complete path segment, then replace that
                            //     prefix with "/" in the input buffer; otherwise,
                            src += 1;
                            buffer[src] = '/'; // Only set because of the Assert above
                            // Don't advance the destination.
                            continue;
                        }

                        break;
                    case 3:
                        ch2 = buffer[src + 1];
                        ch3 = buffer[src + 2];

                        if (ch2 == '/')
                        {
                            src += 1;
                            // Duplicate slash, don't advance the destination.
                            continue;
                        }
                        else if (ch2 == '.' && ch3 == '.')
                        {
                            // C.  if the input buffer begins with a prefix of "/../" or "/..",
                            //     where ".." is a complete path segment, then replace that
                            //     prefix with "/" in the input buffer and remove the last
                            //     segment and its preceding "/" (if any) from the output
                            //     buffer; otherwise,
                            src += 2;
                            buffer[src] = '/';  // Only set because of the Assert above

                            if (dst > 0)
                            {
                                do
                                {
                                    dst--;
                                } while (dst > 0 && buffer[dst] != '/');
                            }

                            continue;
                        }
                        else if (ch2 == '.' && ch3 == '/')
                        {
                            // B.  if the input buffer begins with a prefix of "/./" or "/.",
                            //     where "." is a complete path segment, then replace that
                            //     prefix with "/" in the input buffer; otherwise,
                            src += 2;
                            continue;
                        }

                        break;
                    default:
                        ch2 = buffer[src + 1];
                        ch3 = buffer[src + 2];
                        ch4 = buffer[src + 3];

                        if (ch2 == '/')
                        {
                            src += 1;
                            // Duplicate slash, don't advance the destination.
                            continue;
                        }
                        else if (ch2 == '.' && ch3 == '.' && ch4 == '/')
                        {
                            // C.  if the input buffer begins with a prefix of "/../" or "/..",
                            //     where ".." is a complete path segment, then replace that
                            //     prefix with "/" in the input buffer and remove the last
                            //     segment and its preceding "/" (if any) from the output
                            //     buffer; otherwise,
                            src += 3;

                            if (dst > 0)
                            {
                                do
                                {
                                    dst--;
                                } while (dst > 0 && buffer[dst] != '/');
                            }

                            continue;
                        }
                        else if (ch2 == '.' && ch3 == '/')
                        {
                            // B.  if the input buffer begins with a prefix of "/./" or "/.",
                            //     where "." is a complete path segment, then replace that
                            //     prefix with "/" in the input buffer; otherwise,
                            src += 2;
                            continue;
                        }

                        break;
                }

                // E.  move the first path segment in the input buffer to the end of
                //     the output buffer, including the initial "/" character (if
                //     any) and any subsequent characters up to, but not including,
                //     the next "/" character or the end of the input buffer.
                do
                {
                    buffer[dst++] = ch1;
                    src++;
                    if (src == buffer.Length)
                    {
                        break;
                    }
                    ch1 = buffer[src];
                } while (ch1 != '/');
            }

            if (dst == 0)
            {
                buffer[dst++] = '/';
            }

            return dst;
        }

        // '/.', '/./' '/..', '/../', '//'
        public static bool ContainsDotSegments(Span<char> buffer)
        {
            var src = 0;
            while (src < buffer.Length)
            {
                var ch1 = buffer[src];
                Debug.Assert(ch1 == '/', "Path segment must always start with a '/'");

                char ch2, ch3, ch4;

                switch (buffer.Length - src)
                {
                    case 1:
                        break;
                    case 2:
                        ch2 = buffer[src + 1];

                        if (ch2 == '.' || ch2 == '/')
                        {
                            return true;
                        }

                        break;
                    case 3:
                        ch2 = buffer[src + 1];
                        ch3 = buffer[src + 2];

                        if (ch2 == '/'
                            || (ch2 == '.' && ch3 == '.') 
                            || (ch2 == '.' && ch3 == '/'))
                        {
                            return true;
                        }

                        break;
                    default:
                        ch2 = buffer[src + 1];
                        ch3 = buffer[src + 2];
                        ch4 = buffer[src + 3];

                        if (ch2 == '/'
                            || (ch2 == '.' && ch3 == '.' && ch4 == '/')
                            || (ch2 == '.' && ch3 == '/'))
                        {
                            return true;
                        }

                        break;
                }

                for (src++; src < buffer.Length && buffer[src] != '/'; src++);
            }

            return false;
        }
    }
}