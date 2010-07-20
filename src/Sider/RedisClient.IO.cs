﻿
using System;
using System.Text;

namespace Sider
{
  // TODO: Detect invalid client state and self-dispose or restart
  //       e.g. when protocol errors occour
  public partial class RedisClient
  {
    // 1st Jan 1970
    public const long UnixEpochL = 621355968000000000L;
    public static readonly DateTime UnixEpoch = new DateTime(UnixEpochL);


    // TODO: use a shared buffer?
    private static byte[] encodeStr(string s) { return Encoding.UTF8.GetBytes(s); }
    private static string decodeStr(byte[] raw) { return Encoding.UTF8.GetString(raw); }

    private static string formatDateTime(DateTime dt)
    {
      return (dt - UnixEpoch).TotalSeconds.ToString();
    }
    private static DateTime parseDateTime(long dateValue)
    {
      return new DateTime(dateValue + UnixEpochL);
    }

    private static string formatTimeSpan(TimeSpan t)
    {
      return t.TotalSeconds.ToString();
    }
    private static TimeSpan parseTimeSpan(byte[] raw)
    {
      var str = Encoding.Default.GetString(raw);

      return TimeSpan.FromSeconds(long.Parse(str));
    }

    private static string formatDouble(double d)
    {
      return double.IsPositiveInfinity(d) ? "+inf" :
        double.IsNegativeInfinity(d) ? "-inf" :
        d.ToString("0.0");
    }
    private static double parseDouble(byte[] raw)
    {
      var str = Encoding.Default.GetString(raw);

      return str == "inf" || str == "+inf" ? double.PositiveInfinity :
        str == "-inf" ? double.NegativeInfinity :
        double.Parse(str);
    }


    private void writeCmd(string command)
    {
      writeCore(w => w.WriteLine(command));
    }

    private void writeCmd(string command, string key)
    {
      writeCore(w => w.WriteLine(command + " " + key));
    }

    private void writeCmd(string command, string key, object param1)
    {
      writeCore(w => w.WriteLine("{0} {1} {2}".F(command, key, param1)));
    }

    private void writeCmd(string command, string key, object param1, object param2)
    {
      writeCore(w => w.WriteLine("{0} {1} {2} {3}".F(command, key, param1, param2)));
    }

    private void writeCmd(string command, string[] keys)
    {
      writeCore(w => w.WriteLine("{0} {1}".F(
        command, string.Join(" ", keys))));
    }

    private void writeCmd(string command, string key, string[] keys)
    {
      writeCore(w => w.WriteLine("{0} {1} {2}".F(
        command, key, string.Join(" ", keys))));
    }

    private void writeCmd(string command, string key, object param, string[] keys)
    {
      writeCore(w => w.WriteLine("{0} {1} {2} {3}".F(
        command, key, param, string.Join(" ", keys))));
    }


    private void writeValue(string command, string key, string value)
    {
      writeCore(w =>
      {
        var raw = encodeStr(value);

        w.WriteLine("{0} {1} {2}".F(command, key, raw.Length));
        w.WriteBulk(raw);
      });
    }

    private void writeValue(string command, string key, object param, string value)
    {
      writeCore(w =>
      {
        var raw = encodeStr(value);

        w.WriteLine("{0} {1} {2} {3}".F(command, key, param, raw.Length));
        w.WriteBulk(raw);
      });
    }


    private int readInt()
    { return readCore(ResponseType.Integer, r => r.ReadNumberLine()); }

    private long readInt64()
    { return readCore(ResponseType.Integer, r => r.ReadNumberLine64()); }

    private bool readBool()
    { return readCore(ResponseType.Integer, r => r.ReadNumberLine() == 1); }

    private bool readOk() { return readStatus("OK"); }

    private bool readStatus(string expectedMsg)
    {
      return readCore(ResponseType.SingleLine, r => r.ReadStatusLine() == expectedMsg);
    }

    private double readDouble()
    {
      return readCore(ResponseType.Bulk, r =>
      {
        var length = r.ReadNumberLine();
        return parseDouble(r.ReadBulk(length));
      });
    }

    private string readBulk()
    {
      return readCore(ResponseType.Bulk, r =>
      {
        var length = r.ReadNumberLine();
        return length < 0 ? null : decodeStr(r.ReadBulk(length));
      });
    }

    private byte[] readBulkRaw()
    {
      return readCore(ResponseType.Bulk, r =>
      {
        var length = r.ReadNumberLine();
        return length < 0 ? null : r.ReadBulk(length);
      });
    }

    private string[] readMultiBulk()
    {
      return readCore(ResponseType.MultiBulk, r =>
      {
        var count = r.ReadNumberLine();
        var result = new string[count];

        for (var i = 0; i < count; i++) {
          var type = _reader.ReadTypeChar();
          Assert.ResponseType(ResponseType.Bulk, type);

          var length = _reader.ReadNumberLine();
          if (length > -1)
            result[i] = decodeStr(_reader.ReadBulk(length));
          else
            result[i] = null;
        }

        return result;
      });
    }


    private void writeCore(Action<RedisWriter> writeAction)
    {
      ensureState();

      try {
        // TODO: Add pipelining support by recording writes
        // TODO: Add logging
        writeAction(_writer);
      }
      catch {
        Dispose();
        throw;
      }
    }

    private T readCore<T>(ResponseType expectedType, Func<RedisReader, T> readFunc)
    {
      ensureState();

      try {
        // TODO: Add pipelining support by recording reads
        // TODO: Add logging
        // TODO: Add error-checking support to reads
        var type = _reader.ReadTypeChar();
        Assert.ResponseType(expectedType, type);

        return readFunc(_reader);
      }
      catch {
        Dispose();
        throw;
      }
    }
  }
}
