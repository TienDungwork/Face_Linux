using Newtonsoft.Json;
using SkiaSharp;
using SocketIOClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ViewFaceCore;
using ViewFaceCore.Configs;
using ViewFaceCore.Core;
using ViewFaceCore.Model;

namespace KTDauDocCCCD
{
    public class Program
    {
        private const string ipAddress = "192.168.5.1";
        private const int port = 8000;
        public bool IsConnected { get; set; }
        public bool IsScan { get; set; }
        public bool Isfail { get; set; }
        public string resultScan { get; set; }
        public event Action getResult;
        private int countThanhCong = 0;
        private int countThatBai = 0;
        public event Action GetInfoConnectDivice;
        private SocketIOClient.SocketIO clientScan;
        private SocketIOClient.SocketIO clientCamera;
        public bool isNewCard = false;
        public bool isReadSuccessfully = false;
        public bool isReadRawDataSuccessfully = false;
        public int readStatus = 1;
        public int ReadCount = 0;
        public int Tot_Count = 0;
        public int Xau_Count = 0;
        public int readMaxTime = 10;
        private long startRead = 0;
        private long ReadDg1 = 0;
        private long ReadOther = 0;
        private long ReadAll = 0;
        public List<Log> LogList = new List<Log>();

        // ViewFaceCore
        private readonly FaceDetector faceDetector = new FaceDetector();
        private readonly FaceLandmarker faceLandmarker = new FaceLandmarker();
        private readonly FaceAntiSpoofing faceAntiSpoofing = new FaceAntiSpoofing();
        private readonly FaceRecognizer faceRecognizer = new FaceRecognizer();
        private FaceTracker faceTracker;
        private byte[] referenceImageBytes = null;
        private float[] referenceFeatures = null;
        private float bestSimilarity = 0f;
        private readonly ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();
        private bool isProcessingQueue = false;
        private long lastProcessedTime = 0;
        private const int maxQueueSize = 5;
        private const int minProcessInterval = 1000;

        public Program()
        {
            // Khởi tạo FaceTracker với kích thước frame thực tế từ camera
            faceTracker = new FaceTracker(new FaceTrackerConfig(1280, 720)
            {   // Kích thước tối thiểu của khuôn mặt
                Threshold = 0.9f,    // Ngưỡng tin cậy
                Stable = true,       // Bật chế độ ổn định
                Interval = 10        // Khoảng thời gian giữa các lần phát hiện
            });
        }

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var program = new Program();
            await program.StartAsync();
            await Task.Delay(Timeout.Infinite); 
        }

        public async Task StartAsync()
        {
            clientScan = new SocketIOClient.SocketIO($"http://{ipAddress}:{port}/");
            clientCamera = new SocketIOClient.SocketIO($"http://{ipAddress}:9000/");
            await ScanStart();
            await TaskCamera();
        }

        private void Reset()
        {
            referenceImageBytes = null;
            referenceFeatures = null;
            bestSimilarity = 0f;
        }

        public async Task ScanStart()
        {
            clientScan.OnConnected += async delegate
            {
                IsConnected = true;
                GetInfoConnectDivice?.Invoke();
            };

            clientScan.On("/event", delegate (SocketIOResponse response)
            {
                resultScan = response.ToString();
                try
                {
                    var payloadList = JsonConvert.DeserializeObject<List<PayLoad>>(response.ToString());
                    if (payloadList == null || payloadList.Count == 0)
                    {
                        Console.WriteLine("Dữ liệu payload rỗng hoặc không hợp lệ.");
                        return;
                    }

                    PayLoad payload = payloadList[0];
                    if (payload == null)
                    {
                        Console.WriteLine("Payload không hợp lệ.");
                        return;
                    }

                    switch (payload.Id)
                    {
                        case "1":
                            isNewCard = true;
                            isReadSuccessfully = false;
                            isReadRawDataSuccessfully = false;
                            readStatus = 1;
                            Reset();
                            startRead = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                            ReadCount++;
                            break;

                        case "2":
                            readStatus = 2;
                            long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                            ReadDg1 = milliseconds - startRead;

                            if (payload.Data == null)
                            {
                                Console.WriteLine("Dữ liệu thẻ không có.");
                                break;
                            }

                            //Console.WriteLine("Nhận dữ liệu thẻ:");
                            //Console.WriteLine($"  Họ tên: {(string.IsNullOrEmpty(payload.Data.PersonName) ? "N/A" : payload.Data.PersonName.ToUpper())}");
                            //Console.WriteLine($"  Ngày sinh: {(string.IsNullOrEmpty(payload.Data.DateOfBirth) ? "N/A" : payload.Data.DateOfBirth)}");
                            //Console.WriteLine($"  Giới tính: {(string.IsNullOrEmpty(payload.Data.Gender) ? "N/A" : payload.Data.Gender)}");
                            //Console.WriteLine($"  Dân tộc: {(string.IsNullOrEmpty(payload.Data.Race) ? "N/A" : payload.Data.Race)}");
                            //Console.WriteLine($"  Quốc tịch: {(string.IsNullOrEmpty(payload.Data.Nationality) ? "N/A" : payload.Data.Nationality)}");
                            //Console.WriteLine($"  Nơi thường trú: {(string.IsNullOrEmpty(payload.Data.ResidencePlace) ? "N/A" : payload.Data.ResidencePlace)}");
                            //Console.WriteLine($"  Số CCCD: {(string.IsNullOrEmpty(payload.Data.IdCard) ? "N/A" : payload.Data.IdCard.ToUpper())}");
                            //Console.WriteLine($"  Ngày cấp: {(string.IsNullOrEmpty(payload.Data.IssueDate) ? "N/A" : payload.Data.IssueDate)}");
                            //Console.WriteLine($"  Ngày hết hạn: {(string.IsNullOrEmpty(payload.Data.ExpiryDate) ? "N/A" : payload.Data.ExpiryDate)}");
                            //Console.WriteLine($"  Số CMND cũ: {(string.IsNullOrEmpty(payload.Data.OldIdCode) ? "N/A" : payload.Data.OldIdCode)}");
                            //Console.WriteLine($"  Tôn giáo: {(string.IsNullOrEmpty(payload.Data.Religion) ? "N/A" : payload.Data.Religion)}");
                            //Console.WriteLine($"  Đặc điểm nhận dạng: {(string.IsNullOrEmpty(payload.Data.PersonalIdentification) ? "N/A" : payload.Data.PersonalIdentification)}");
                            //Console.WriteLine($"  Quê quán: {(string.IsNullOrEmpty(payload.Data.OriginPlace) ? "N/A" : payload.Data.OriginPlace)}");
                            //Console.WriteLine($"  Tên bố: {(string.IsNullOrEmpty(payload.Data.FatherName) ? "N/A" : payload.Data.FatherName)}");
                            //Console.WriteLine($"  Tên mẹ: {(string.IsNullOrEmpty(payload.Data.MotherName) ? "N/A" : payload.Data.MotherName)}");
                            //Console.WriteLine($"  Tên vợ/chồng: {(string.IsNullOrEmpty(payload.Data.WifeName) ? "N/A" : payload.Data.WifeName)}");
                            break;

                        case "4":
                            readStatus = 4;
                            long milliseconds_3 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                            ReadAll = milliseconds_3 - startRead;
                            ReadOther = ReadAll - ReadDg1;
                            Tot_Count++;

                            if (payload.Data == null || string.IsNullOrEmpty(payload.Data.ImgData))
                            {
                                //Console.WriteLine("Không nhận được dữ liệu ảnh hoặc dữ liệu thẻ.");
                                break;
                            }

                            byte[] bytes = Convert.FromBase64String(payload.Data.ImgData);
                            referenceImageBytes = bytes;
                            ProcessReferenceImage(bytes);
                            //Console.WriteLine("Đã xử lý ảnh thẻ.");

                            Log log = new Log
                            {
                                serial = "00000000000",
                                time = DateTimeOffset.FromUnixTimeMilliseconds(startRead).LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss"),
                                status = new TestStatus
                                {
                                    ReadBeginTime = startRead,
                                    ReadDg1Time = ReadDg1,
                                    ReadOtherTime = ReadOther,
                                    ReadAllTime = ReadAll,
                                    ReadStatus = "Read all data successfully!"
                                },
                                persion = new Persion
                                {
                                    SoCCCD = string.IsNullOrEmpty(payload.Data.IdCard) ? "N/A" : payload.Data.IdCard.ToUpper(),
                                    HoVaTen = string.IsNullOrEmpty(payload.Data.PersonName) ? "N/A" : payload.Data.PersonName.ToUpper()
                                }
                            };
                            LogList.Add(log);
                            //Console.WriteLine($"Thêm log: Thành công - CCCD: {log.persion.SoCCCD}, Thời gian: {log.time}");
                            break;

                        case "3":
                            Xau_Count++;
                            Log log1 = new Log
                            {
                                serial = "00000000000",
                                time = DateTimeOffset.FromUnixTimeMilliseconds(startRead).LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss"),
                                status = new TestStatus
                                {
                                    ReadBeginTime = startRead,
                                    ReadDg1Time = ReadDg1,
                                    ReadOtherTime = ReadOther,
                                    ReadAllTime = ReadAll,
                                    ReadStatus = payload.Message ?? "N/A"
                                },
                                persion = new Persion
                                {
                                    SoCCCD = payload.Data?.IdCard?.ToUpper() ?? "N/A",
                                    HoVaTen = payload.Data?.PersonName?.ToUpper() ?? "N/A"
                                }
                            };
                            LogList.Add(log1);
                            readStatus = 3;
                            //Console.WriteLine($"Thêm log: Thất bại - Thông báo: {payload.Message ?? "N/A"}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Lỗi xử lý dữ liệu socket: {ex.Message}");
                }
            });

            // Lắng nghe phản hồi từ máy chủ
            clientScan.On("response", response =>
            {
                try
                {
                    var responseData = JsonConvert.DeserializeObject<dynamic>(response.ToString());
                    //Console.WriteLine($"Phản hồi từ máy chủ: {responseData.status} - {responseData.message}");
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Lỗi xử lý phản hồi từ máy chủ: {ex.Message}");
                }
            });

            clientScan.OnError += async delegate
            {
                IsConnected = false;
                //Console.WriteLine("Lỗi máy quét.");
                GetInfoConnectDivice?.Invoke();
            };

            clientScan.OnDisconnected += async delegate
            {
                IsConnected = false;
                //Console.WriteLine("Ngắt kết nối máy quét.");
                GetInfoConnectDivice?.Invoke();
            };

            await clientScan.ConnectAsync();
        }

        public async Task TaskCamera()
        {
            clientCamera.OnConnected += async delegate
            {
                //Console.WriteLine("Kết nối camera thành công.");
                GetInfoConnectDivice?.Invoke();
                Task.Run(() => ProcessFrameQueue());
            };

            clientCamera.On("/image", delegate (SocketIOResponse response)
            {
                resultScan = response.ToString();
                try
                {
                    var infoList = JsonConvert.DeserializeObject<List<CCamera>>(resultScan);
                    if (infoList == null || infoList.Count == 0 || string.IsNullOrEmpty(infoList[0]?.data))
                    {
                        Console.WriteLine("Dữ liệu camera không hợp lệ.");
                        return;
                    }

                    byte[] frameBytes = Convert.FromBase64String(infoList[0].data);
                    if (frameQueue.Count < maxQueueSize)
                    {
                        frameQueue.Enqueue(frameBytes);
                    }
                    else
                    {
                        frameQueue.TryDequeue(out _);
                        frameQueue.Enqueue(frameBytes);
                    }
                    //Console.WriteLine("Nhận khung hình camera.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi xử lý dữ liệu camera: {ex.Message}");
                }
            });

            clientCamera.On("/info", delegate (SocketIOResponse response)
            {
                //Console.WriteLine("Nhận thông tin camera.");
            });

            clientCamera.OnError += async delegate
            {
                //Console.WriteLine("Lỗi camera.");
            };

            clientCamera.OnDisconnected += async delegate
            {
                //Console.WriteLine("Ngắt kết nối camera.");
            };

            await clientCamera.ConnectAsync();
        }

        private void ProcessReferenceImage(byte[] imageBytes)
        {
            try
            {
                using (SKBitmap bitmap = ByteArrayToSKBitmap(imageBytes))
                {
                    if (bitmap == null)
                    {
                        //Console.WriteLine("Ảnh thẻ CCCD không hợp lệ.");
                        return;
                    }

                    using (FaceImage faceImage = bitmap.ToFaceImage())
                    {
                        var faceInfos = faceDetector.Detect(faceImage);
                        if (faceInfos == null || faceInfos.Length == 0)
                        {
                            //Console.WriteLine("Không phát hiện khuôn mặt trong ảnh CCCD.");
                            return;
                        }

                        var faceInfo = faceInfos[0];
                        var points = faceLandmarker.Mark(faceImage, faceInfo);
                        referenceFeatures = faceRecognizer.Extract(faceImage, points);
                        if (referenceFeatures == null)
                        {
                            //Console.WriteLine("Không thể trích xuất đặc trưng từ ảnh CCCD.");
                            return;
                        }

                        //Console.WriteLine("Trích xuất đặc trưng ảnh CCCD thành công.");
                    }
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Lỗi xử lý ảnh CCCD: {ex.Message}");
            }
        }

        private void ProcessFrameQueue()
        {
            isProcessingQueue = true;
            long? startTime = null;
            bestSimilarity = 0f;

            while (isProcessingQueue)
            {
                long currentTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                if (currentTime - lastProcessedTime < minProcessInterval)
                {
                    Task.Delay(10).Wait();
                    continue;
                }

                if (frameQueue.TryDequeue(out byte[] frameBytes) && referenceFeatures != null)
                {
                    try
                    {
                        if (!startTime.HasValue)
                        {
                            startTime = currentTime;
                        }

                        using (SKBitmap bitmap = ByteArrayToSKBitmap(frameBytes))
                        {
                            if (bitmap == null)
                            {
                                continue;
                            }

                            using (FaceImage faceImage = bitmap.ToFaceImage())
                            {
                                // Sử dụng FaceTracker để theo dõi khuôn mặt
                                var trackResults = faceTracker.Track(faceImage);

                                if (trackResults == null || trackResults.Length == 0)
                                {
                                    // Kiểm tra timeout nếu không phát hiện được khuôn mặt
                                    if (startTime.HasValue && (currentTime - startTime.Value >= 10000))
                                    {
                                        countThatBai++;
                                        if (LogList.Count > 0)
                                        {
                                            var latestLog = LogList[LogList.Count - 1];
                                            SendFaceVerificationResult(latestLog, "Failed", bestSimilarity, "Không phát hiện khuôn mặt").GetAwaiter().GetResult();
                                        }
                                        startTime = null;
                                        bestSimilarity = 0f;
                                        referenceFeatures = null;
                                        faceTracker.Reset(); // Reset tracker khi timeout
                                    }
                                    continue;
                                }

                                // Lấy khuôn mặt đầu tiên được theo dõi
                                var faceInfo = trackResults[0].ToFaceInfo();
                                var points = faceLandmarker.Mark(faceImage, faceInfo);
                                var features = faceRecognizer.Extract(faceImage, points);

                                if (features == null)
                                {
                                    continue;
                                }

                                float similarity = CalculateSimilarity(features, referenceFeatures) * 100;
                                if (similarity > bestSimilarity)
                                {
                                    bestSimilarity = similarity;
                                }

                                if (bestSimilarity >= 75f)
                                {
                                    Console.WriteLine($"Xác thực khuôn mặt thành công: Độ tương đồng: {bestSimilarity:F0}%");
                                    countThanhCong++;

                                    if (LogList.Count > 0)
                                    {
                                        var latestLog = LogList[LogList.Count - 1];
                                        SendFaceVerificationResult(latestLog, "Success", bestSimilarity, "Xác thực thành công").GetAwaiter().GetResult();
                                    }

                                    startTime = null;
                                    bestSimilarity = 0f;
                                    referenceFeatures = null;
                                    faceTracker.Reset(); // Reset tracker khi xác thực thành công
                                    continue;
                                }

                                if (startTime.HasValue && (currentTime - startTime.Value >= 3000))
                                {
                                    Console.WriteLine($"Xác thực khuôn mặt thất bại: Độ tương đồng cao nhất: {bestSimilarity:F0}%");
                                    countThatBai++;

                                    if (LogList.Count > 0)
                                    {
                                        var latestLog = LogList[LogList.Count - 1];
                                        SendFaceVerificationResult(latestLog, "Failed", bestSimilarity, "Xác thực thất bại").GetAwaiter().GetResult();
                                    }

                                    startTime = null;
                                    bestSimilarity = 0f;
                                    referenceFeatures = null;
                                    faceTracker.Reset(); // Reset tracker khi xác thực thất bại
                                    continue;
                                }

                                lastProcessedTime = currentTime;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi xử lý khung hình webcam: {ex.Message}");
                    }
                }
                else
                {
                    Task.Delay(10).Wait();
                }
            }
        }

        private async Task SendFaceVerificationResult(Log log, string status, float similarity, string message)
        {
            if (!IsConnected)
            {
                Console.WriteLine("Không thể gửi dữ liệu: Máy quét không được kết nối.");
                return;
            }

            try
            {
                var resultData = new
                {
                    id = 8,
                    data = new
                    {
                        message = status == "Success" ? "Xác thực thành công" : "Xác thực thất bại",
                        Similarity = similarity
                    }
                };

                string jsonData = JsonConvert.SerializeObject(resultData, Formatting.Indented);
                //Console.WriteLine($"Chuẩn bị gửi JSON: \n{jsonData}");
                await clientScan.EmitAsync("/event", jsonData);

                //Console.WriteLine($"Đã gửi kết quả nhận diện khuôn mặt: {status}, Độ tương đồng: {similarity:F0}%");
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Lỗi khi gửi kết quả nhận diện khuôn mặt: {ex.Message}");
            }
        }

        private SKBitmap ByteArrayToSKBitmap(byte[] data)
        {
            try
            {
                if (data == null || data.Length < 10) return null;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    return SKBitmap.Decode(ms);
                }
            }
            catch
            {
                return null;
            }
        }

        private float CalculateSimilarity(float[] features1, float[] features2)
        {
            if (features1 == null || features2 == null || features1.Length != features2.Length) return 0f;
            float dotProduct = 0, norm1 = 0, norm2 = 0;
            for (int i = 0; i < features1.Length; i++)
            {
                dotProduct += features1[i] * features2[i];
                norm1 += features1[i] * features1[i];
                norm2 += features2[i] * features2[i];
            }
            float similarity = dotProduct / (float)(Math.Sqrt(norm1) * Math.Sqrt(norm2));
            return float.IsNaN(similarity) ? 0f : similarity;
        }

        public class PayLoad
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("data")]
            public IcaoInfoCard Data { get; set; }
        }

        public class IcaoInfoCard
        {
            [JsonProperty("idCode")]
            public string IdCard { get; set; }

            [JsonProperty("personName")]
            public string PersonName { get; set; }

            [JsonProperty("dateOfBirth")]
            public string DateOfBirth { get; set; }

            [JsonProperty("gender")]
            public string Gender { get; set; }

            [JsonProperty("nationality")]
            public string Nationality { get; set; }

            [JsonProperty("race")]
            public string Race { get; set; }

            [JsonProperty("religion")]
            public string Religion { get; set; }

            [JsonProperty("originPlace")]
            public string OriginPlace { get; set; }

            [JsonProperty("residencePlace")]
            public string ResidencePlace { get; set; }

            [JsonProperty("personalIdentification")]
            public string PersonalIdentification { get; set; }

            [JsonProperty("issueDate")]
            public string IssueDate { get; set; }

            [JsonProperty("expiryDate")]
            public string ExpiryDate { get; set; }

            [JsonProperty("fatherName")]
            public string FatherName { get; set; }

            [JsonProperty("motherName")]
            public string MotherName { get; set; }

            [JsonProperty("wifeName")]
            public string WifeName { get; set; }

            [JsonProperty("oldIdCode")]
            public string OldIdCode { get; set; }

            [JsonProperty("img_data")]
            public string ImgData { get; set; }

            [JsonProperty("dg2")]
            public string Dg2 { get; set; }

            [JsonProperty("dg13")]
            public string Dg13 { get; set; }

            [JsonProperty("dg14")]
            public string Dg14 { get; set; }

            [JsonProperty("dg15")]
            public string Dg15 { get; set; }

            [JsonProperty("sod")]
            public string Sod { get; set; }
        }

        public class CCamera
        {
            [JsonProperty("data")]
            public string data { get; set; }
        }

        public class Persion
        {
            public string SoCCCD;
            public string SoCMND;
            public string HoVaTen;
            public string NgaySinh;
            public string GioiTinh;
            public string NgayCap;
            public string NgayHetHan;
            public string QuocTich;
            public string DanToc;
            public string TonGiao;
            public string HoVaTenBo;
            public string HoVaTenMe;
            public string QueQuan;
            public string NoiThuongTru;
            public string DacDiemNhanDang;
            public Persion()
            {
                SoCCCD = "";
                SoCMND = "";
                HoVaTen = "";
                NgaySinh = "";
                GioiTinh = "";
                NgayCap = "";
                NgayHetHan = "";
                QuocTich = "";
                DanToc = "";
                TonGiao = "";
                HoVaTenBo = "";
                HoVaTenMe = "";
                QueQuan = "";
                NoiThuongTru = "";
                DacDiemNhanDang = "";
            }
        }

        public class TestStatus
        {
            public long ReadBeginTime;
            public long ReadDg1Time;
            public long ReadOtherTime;
            public long ReadAllTime;
            public string ReadStatus;
            public TestStatus()
            {
                ReadBeginTime = 0;
                ReadDg1Time = 0;
                ReadOtherTime = 0;
                ReadAllTime = 0;
                ReadStatus = "";
            }
        }

        public class Log
        {
            public string serial;
            public string time;
            public TestStatus status;
            public Persion persion;
            public Log()
            {
                serial = "0000000000";
                time = "";
                status = new TestStatus();
                persion = new Persion();
            }
        }
    }
}