using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System;

namespace Ntp
{
    // 윈도우 시간을 변경하기 위해서는

    // 코드 실행 전 [관리자 권한] 속성으로 만들어야 함.

    // 프로젝트 속성 -> 보안 ->  -> ClickOnce 보안 설정 사용 체크 하기 -> [app.manifest] 파일 열기!

    // UAC 매니페스트 옵션 -> <requestedExecutionLevel  level="requireAdministrator" uiAccess="false" />로 변경

    // 프로젝트 속성 -> 보안 ->  -> ClickOnce 보안 설정 사용 체크 해제! 

    public partial class NtpClient : IDisposable
    {
        public NtpClient()
        {
            _disposeValue = false;

            Interlocked.Exchange(ref _isStarted, 0);

            SetupNtpClient(); // NTP 클라이언트 설정 명령어 [수행]
            SetTimeZone(720);
            StartPeriodicTimeSynchronization(); // 주기적 [시간 동기화] 실행 함수 [호출]
        }

        ~NtpClient() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposeValue)
            {
                if (disposing)
                {
                    StopPeriodicTimeSynchronization(); // 주기적 [시간 동기화] 중단 함수 [호출]
                }

            }

        }

        [DllImport("kernel32.dll", SetLastError = true)] // WinAPI("Kernel32 dll") 사용 코드
        // SetTimeZoneInformation() 함수: 표준 시간(UTC)에서 현지 시간으로의 변환 제어
        private static extern bool SetTimeZoneInformation([In] ref TimeZoneInformation timeZoneInformation);

        // 구조체 정의: TimeZoneInformation
        private struct TimeZoneInformation
        {
            public int Bias; // 멤버 변수 [Bias] 정의
            public int StandardBias;
            public int DaylightBias;
            public SystemTime StandardDate;
            public SystemTime DaylightDate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SystemTime
        {
            public ushort Year;
            public ushort Month;
            public ushort DayOfWeek;
            public ushort Day;
            public ushort Hour;
            public ushort Minute;
            public ushort Second;
            public ushort Milliseconds;
        }

        public static void SetTimeZone(int zone)
        {
            // 시스템 시간 수동 설정 명령어 수행
            //
            // 새로운 [TimeZoneInformation] 객체 tzi 생성
            var tzi = new TimeZoneInformation
            {
                // 1. [시간] 단위로 변환
                //Bias = -(zone * 60), // 바이어스(분) = -(현지 시간 * 60)

                // 2. [분] 단위 유지
                Bias = -zone, // 바이어스(분) = -(현지 시간)
            };

            // # [타임존] 체크 기능 수행
            //
            // 1. 타임존 시간이 (- 720) 이상인 경우 (-12시간)
            // 2. 타임존 시간이 (+ 840) 이상인 경우 (+14시간)
            // 3. 타임존 범위 = (- 720) ~ (+ 840)까지 [1, 2]의 조건 모두를 만족하면서
            // 4. [tzi] 객체를 참조하는 SetTimeZoneInformation() 함수 호출
            //
            if (SetTimeZoneInformation(ref tzi))
            {
                Console.WriteLine("The timeZone" + "[" + zone + "]" + "is now valid.");
                return;
            }
            Console.WriteLine("The timeZone" + "[" + zone + "]" + "is not valid.");
        }

        public static void SetupNtpClient()
        {
            try
            {
                // NTP 클라이언트 설정 명령어 수행
                //
                // #. [Process] 클래스
                // 1. 로컬 및 원격 프로세스에 대한 액세스 제공
                // 2. 로컬 시스템 프로세스의 시작 및 중지 기능
                //
                Process proc = new Process(); // 새로운 Process 객체 [proc] 생성

                proc.StartInfo.FileName = "cmd.exe"; // 명령 프롬프트(cmd) 실행

                // 1. 기존 NTP 서버에서 원하는 NTP 서버로 설정 (EX. 도메인 서버 IP: time.windows.com)
                proc.StartInfo.Arguments = "/c w32tm /config /syncfromflags:manual /manualpeerlist:time.windows.com /update";
                proc.StartInfo.UseShellExecute = false; // 셸 사용 (X) => 직접 명령어 호출 (O)
                proc.StartInfo.RedirectStandardOutput = true; // 호출 [결과] 텍스트 출력 (O)
                proc.Start(); // 프로세스 시작

                string output1 = proc.StandardOutput.ReadToEnd(); // [결과] 텍스트 1을[string]으로 저장

                Console.WriteLine(output1); // [결과] 텍스트 1 출력

                if (proc.ExitCode != 0)
                {
                    Console.WriteLine("You failed to set [desiring NTP server]!");
                }
                Thread.Sleep(1); // [1초] 지연

                // 2. Windows Time 서비스가 자동으로 시작되도록 설정
                proc.StartInfo.Arguments = "/c sc config w32time start=auto";
                proc.StartInfo.UseShellExecute = false; // 셸 사용 (X) => 직접 명령어 호출 (O)
                proc.StartInfo.RedirectStandardOutput = true; // 호출 [결과] 텍스트 출력 (O)
                proc.Start(); // 프로세스 시작

                string output2 = proc.StandardOutput.ReadToEnd(); // [결과] 텍스트 2를 [string]으로 저장

                Console.WriteLine(output2); // [결과] 텍스트 2 출력

                if (proc.ExitCode != 0)
                {
                    Console.WriteLine("You failed [Windows Time Service] to start automatically!");
                }
                Thread.Sleep(1); // [1초] 지연

                // 3. 재부팅 후, Windows time 서비스가 자동으로 시작되지 않는 문제가 해결되도록 설정
                proc.StartInfo.Arguments = "/c sc triggerinfo w32time start/networkon stop/networkoff";
                proc.StartInfo.UseShellExecute = false; // 셸 사용 (X) -=> 직접 명령어 호출 (O)
                proc.Start(); // 프로세스 시작

                string output3 = proc.StandardOutput.ReadToEnd(); // [결과] 텍스트 3을 [string]으로 저장

                Console.WriteLine(output3);

                if (proc.ExitCode != 0)
                {
                    Console.WriteLine("You failed [Windows Time Service] to start automatically after a reboot!");
                }
                Thread.Sleep(1); // [1초] 지연

                // 4-1. Windows Time 서비스 중지
                proc.StartInfo.Arguments = "/c net stop w32time"; // 명령어 4_1 설정
                proc.StartInfo.UseShellExecute = false; // 셸 사용 (X) => 직접 명령어 호출 (O)
                proc.StartInfo.RedirectStandardOutput = true; // 호출 [결과] 텍스트 출력 (O)
                proc.Start(); // 프로세스 시작

                string output4_1 = proc.StandardOutput.ReadToEnd(); // [결과] 텍스트 4_1을 [string]으로 저장

                Console.WriteLine(output4_1);

                // 4-1. Command 오류 문구 출력
                //
                if (proc.ExitCode != 0)
                {
                    Console.WriteLine("You failed to stop [Windows Time Service]!");
                }
                Thread.Sleep(1); // [1초] 지연

                // 4-2. Windows Time 서비스 시작
                proc.StartInfo.Arguments = "/c net start w32time"; // 명령어 4_2 설정
                proc.StartInfo.UseShellExecute = false; // 셸 사용 (X) => 직접 명령어 호출 (O)
                proc.StartInfo.RedirectStandardOutput = true; // 호출 [결과] 텍스트 출력 (O)
                proc.Start(); // 프로세스 시작

                string output4_2 = proc.StandardOutput.ReadToEnd(); // [결과] 텍스트 4_2을 [string]으로 저장

                Console.WriteLine(output4_2);

                // 4-1. Command 오류 문구 출력
                //
                if (proc.ExitCode != 0)
                {
                    Console.WriteLine("You failed to start [Windows Time Service]!");
                }
                Thread.Sleep(1); // [1초] 지연

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + ": 관리자 권한으로 실행하세요.");
                Console.Read();
                Environment.Exit(0);
            }
            Console.WriteLine("관리자 권한으로 실행 완료!");
            Console.WriteLine();
        }

        // 주기적 [시간 동기화] 실행 함수
        public void StartPeriodicTimeSynchronization()
        {
            if (Interlocked.Read(ref _isStarted) == 0)
            {
                Interlocked.Exchange(ref _isStarted, 1);
                _timeSynchronizionthread = new Thread(() =>
                {
                    while (Interlocked.Read(ref _isStarted) == 1)
                    {
                        // 무한 루프: [1초] 주기로 -> [NTP 서버] 동기화
                        while (true)
                        {
                            SynchronizeNtpTime(); // [시간 동기화] 수행
                            Thread.Sleep(1); // [1초] 지연

                            // 스레드 출력
                            Console.WriteLine($"[{new StackTrace(true).GetFrame(0).GetMethod().Name}:"
                                + $"{new StackTrace(true).GetFrame(0).GetFileLineNumber()}] thread call");
                        }

                    }

                })
                {
                    IsBackground = false
                };
                _timeSynchronizionthread.Start(); // 시간 동기화 [스레드] 시작
            }

        }

        // 주기적 [시간 동기화] 시작 함수
        public static void SynchronizeNtpTime()
        {

            try
            {
                // NTP 수동 동기화 명령어 수행
                //
                // [Process] 클래스
                // 1. 로컬 및 원격 프로세스에 대한 액세스 제공
                // 2. 로컬 시스템 프로세스의 시작 및 중지 가능
                //
                Process proc = new Process(); // 새로운 Process 객체 [proc] 생성

                proc.StartInfo.FileName = "cmd.exe";

                // 5. [시간 동기화] 수행
                proc.StartInfo.Arguments = "/c w32tm /resync"; // 명령어 5 설정
                proc.StartInfo.UseShellExecute = false; // 셸 사용 (X) => 직접 명령어 호출 (O)
                proc.StartInfo.RedirectStandardOutput = true; // 호출 [결과] 텍스트 출력 (O)
                proc.Start(); // 프로세스 시작

                string output5 = proc.StandardOutput.ReadToEnd(); // [결과] 텍스트 5를 [string]으로 저장

                Console.WriteLine(output5);

                // 5. Command 오류 문구 출력
                //
                if (proc.ExitCode != 0)
                {
                    Console.WriteLine("You failed to carry out [Time Synchronization]!");
                }
                Thread.Sleep(1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + ": 관리자 권한으로 실행하세요.");
                Console.Read();
                Environment.Exit(0);
            }
            Console.WriteLine("관리자 권한으로 실행 완료!");
            Console.WriteLine();
        }

        // 주기적 [시간 동기화] 중단 함수
        public void StopPeriodicTimeSynchronization()
        {
            if (Interlocked.Read(ref _isStarted) == 1)
            {
                Interlocked.Exchange(ref _isStarted, 0);
                _timeSynchronizionthread?.Join();
                _timeSynchronizionthread = null;
            }

        }
        private Thread _timeSynchronizionthread;
        private long _isStarted;
        private readonly bool _disposeValue;
    }

}