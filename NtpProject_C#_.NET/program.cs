// See [https://aka.ms/new-console-template] For More Information!

using System;
using System.Threading;

namespace Ntp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            NtpClient ntpClient = new NtpClient(); // NtpClient 객체 생성
            NtpClient.SetTimeZone(540); // Ex. (UTC + 9): 서울 표준 시간대(UTC) 호출
            Console.WriteLine(); // 한 줄 띄어쓰기!
            Thread.Sleep(1); // [1초] 지연
        }

    }

}