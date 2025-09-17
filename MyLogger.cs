using NLog;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace TorchPlugin
{
    internal class MyLogger
    {
        private const int MaxExceptionDepth = 100;

        ThreadLocal<StringBuilder> _stringBuilderLocal = new ThreadLocal<StringBuilder>();

        readonly string _logEntryPrefix;
        readonly Logger _logger;
        public MyLogger(string name, string logEntryName)
        {
            _logEntryPrefix = $"[{logEntryName}] ";
            _logger = LogManager.GetLogger(name);
        }

        public bool IsTraceEnabled => _logger.IsTraceEnabled;
        public bool IsDebugEnabled => _logger.IsDebugEnabled;
        public bool IsInfoEnabled => _logger.IsInfoEnabled;
        public bool IsWarningEnabled => _logger.IsWarnEnabled;
        public bool IsErrorEnabled => _logger.IsErrorEnabled;
        public bool IsCriticalEnabled => _logger.IsFatalEnabled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Trace(Exception ex, string message)
        {
            if (!IsTraceEnabled)
                return;

            _logger.Trace(Format(ex, message));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Debug(Exception ex, string message)
        {
            if (!IsDebugEnabled)
                return;

            _logger.Debug(Format(ex, message));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Info(Exception ex, string message)
        {
            if (!IsInfoEnabled)
                return;

            _logger.Info(Format(ex, message));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Warning(Exception ex, string message)
        {
            if (!IsWarningEnabled)
                return;

            _logger.Warn(Format(ex, message));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Error(Exception ex, string message)
        {
            if (!IsErrorEnabled)
                return;

            _logger.Error(Format(ex, message));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Critical(Exception ex, string message)
        {
            if (!IsCriticalEnabled)
                return;

            _logger.Fatal(Format(ex, message));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Trace(string message, params object[] data) => _logger.Trace(message, data);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Debug(string message, params object[] data) => _logger.Debug(message, data);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Info(string message, params object[] data) => _logger.Info(message, data);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Warning(string message, params object[] data) => _logger.Warn(message, data);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Error(string message, params object[] data) => _logger.Error(message, data);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Critical(string message, params object[] data) => _logger.Fatal(message, data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        string Format(Exception exception, string message)
        {
            var stringBuilder = _stringBuilderLocal.Value;

            if (stringBuilder == null)
                stringBuilder = _stringBuilderLocal.Value = new StringBuilder();

            if (message == null)
                message = string.Empty;

            stringBuilder.Append(_logEntryPrefix);

            stringBuilder.Append(message);

            FormatException(stringBuilder, exception);

            var result = stringBuilder.ToString();
            stringBuilder.Clear();

            return result;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FormatException(StringBuilder sb, Exception ex)
        {
            if (ex == null)
                return;

            for (var i = 0; i < MaxExceptionDepth; i++)
            {
                sb.Append("\r\n[");
                sb.Append(ex.GetType().Name);
                sb.Append("] ");
                sb.Append(ex.Message);

                if (ex.TargetSite != null)
                {
                    sb.Append("\r\nMethod: ");
                    sb.Append(ex.TargetSite);
                }

                if (ex.Data.Count > 0)
                {
                    sb.Append("\r\nData:");
                    foreach (var key in ex.Data.Keys)
                    {
                        sb.Append("\r\n");
                        sb.Append(key);
                        sb.Append(" = ");
                        sb.Append(ex.Data[key]);
                    }
                }

                sb.Append("\r\nTraceback:\r\n");
                sb.Append(ex.StackTrace);

                ex = ex.InnerException;
                if (ex == null)
                    return;

                sb.Append("\r\nInner exception:\r\n");
            }

            sb.Append($"WARNING: Not logging more than {MaxExceptionDepth} inner exceptions");
        }
    }
}
