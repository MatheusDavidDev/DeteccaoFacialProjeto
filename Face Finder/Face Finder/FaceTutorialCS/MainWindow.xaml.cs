using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace FaceTutorial
{
    public partial class MainWindow : Window
    {
        // Substitua <SubscriptionKey> pela sua chave de assinatura v�lida.
        // Por exemplo, subscriptionKey = "0123456789abcdef0123456789ABCDEF"
        private const string subscriptionKey = "<Coloque aqui sua key>";

        // Substitua ou verifique a regi�o.
        //
        // Voc� deve usar a mesma regi�o que usou para obter sua assinatura
        // chaves. Por exemplo, se voc� obteve suas chaves de assinatura do
        // regi�o westus, substitua "Westcentralus" por "Westus".
        //
        // NOTA: As chaves de assinatura de teste gratuito s�o geradas no westcentralus
        // regi�o, ent�o se voc� estiver usando uma chave de assinatura de teste gratuito, voc� deve
        // n�o precisa mudar esta regi�o.
        private const string faceEndpoint =
            "<Coloque aqui seu link do Face API>";

        private readonly IFaceClient faceClient = new FaceClient(
            new ApiKeyServiceClientCredentials(subscriptionKey),
            new System.Net.Http.DelegatingHandler[] { });

        // A lista de rostos detectados.
        private IList<DetectedFace> faceList;

        // A lista de descri��es dos rostos detectados.
        private string[] faceDescriptions;

        // O fator de redimensionamento da imagem exibida.
        private double resizeFactor;

        private const string defaultStatusBarText =
            "Posicione o ponteiro do mouse sobre um rosto para ver sua descri��o.";

        public MainWindow()
        {
            InitializeComponent();

            if (Uri.IsWellFormedUriString(faceEndpoint, UriKind.Absolute))
            {
                faceClient.Endpoint = faceEndpoint;
            }
            else
            {
                MessageBox.Show(faceEndpoint,
                    "Invalid URI", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }
        }

        // Exibe a imagem e chama UploadAndDetectFaces.
        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Obtenha o arquivo de imagem a ser digitalizado do usu�rio.
            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            // Retorna se cancelado.
            if (!(bool)result)
            {
                return;
            }

            // Exibe o arquivo de imagem.
            string filePath = openDlg.FileName;

            Uri fileUri = new Uri(filePath);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;

            // Detecta qualquer rosto na imagem.
            Title = "Detectando imagem...";
            faceList = await UploadAndDetectFaces(filePath);
            Title = String.Format(
                "Detec��o finalizada. {0} Rosto(s) encontrados", faceList.Count);

            if (faceList.Count > 0)
            {
                // Prepare-se para desenhar ret�ngulos ao redor dos rostos.
                DrawingVisual visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(bitmapSource,
                    new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));
                double dpi = bitmapSource.DpiX;

                // Algumas imagens n�o cont�m informa��es de dpi.
                resizeFactor = (dpi == 0) ? 1 : 96 / dpi;
                faceDescriptions = new String[faceList.Count];

                for (int i = 0; i < faceList.Count; ++i)
                {
                    DetectedFace face = faceList[i];

                    // Desenhe um ret�ngulo no rosto.
                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(Brushes.Red, 2),
                        new Rect(
                            face.FaceRectangle.Left * resizeFactor,
                            face.FaceRectangle.Top * resizeFactor,
                            face.FaceRectangle.Width * resizeFactor,
                            face.FaceRectangle.Height * resizeFactor
                            )
                    );

                    // Armazena a descri��o do rosto.
                    faceDescriptions[i] = FaceDescription(face);
                }

                drawingContext.Close();

                // Exibe a imagem com o ret�ngulo ao redor do rosto.
                RenderTargetBitmap faceWithRectBitmap = new RenderTargetBitmap(
                    (int)(bitmapSource.PixelWidth * resizeFactor),
                    (int)(bitmapSource.PixelHeight * resizeFactor),
                    96,
                    96,
                    PixelFormats.Pbgra32);

                faceWithRectBitmap.Render(visual);
                FacePhoto.Source = faceWithRectBitmap;

                // Defina o texto da barra de status.
                faceDescriptionStatusBar.Text = defaultStatusBarText;
            }
        }

        // Exibe a descri��o da face quando o mouse est� sobre um ret�ngulo de face.
        private void FacePhoto_MouseMove(object sender, MouseEventArgs e)
        {
            // Se a chamada REST n�o foi conclu�da, retorne.
            if (faceList == null)
                return;

            // Encontre a posi��o do mouse em rela��o � imagem.
            Point mouseXY = e.GetPosition(FacePhoto);

            ImageSource imageSource = FacePhoto.Source;
            BitmapSource bitmapSource = (BitmapSource)imageSource;

            // Ajuste de escala entre o tamanho real e o tamanho exibido.
            var scale = FacePhoto.ActualWidth / (bitmapSource.PixelWidth / resizeFactor);

            // Verifique se a posi��o do mouse est� sobre um ret�ngulo de face.
            bool mouseOverFace = false;

            for (int i = 0; i < faceList.Count; ++i)
            {
                FaceRectangle fr = faceList[i].FaceRectangle;
                double left = fr.Left * scale;
                double top = fr.Top * scale;
                double width = fr.Width * scale;
                double height = fr.Height * scale;

                // Exibe a descri��o da face se o mouse estiver sobre este ret�ngulo de face.
                if (mouseXY.X >= left && mouseXY.X <= left + width &&
                    mouseXY.Y >= top && mouseXY.Y <= top + height)
                {
                    faceDescriptionStatusBar.Text = faceDescriptions[i];
                    mouseOverFace = true;
                    break;
                }
            }

            // String a ser exibida quando o mouse n�o est� sobre um ret�ngulo de face.
            if (!mouseOverFace) faceDescriptionStatusBar.Text = defaultStatusBarText;
        }

        // Carrega o arquivo de imagem e chama DetectWithStreamAsync.
        private async Task<IList<DetectedFace>> UploadAndDetectFaces(string imageFilePath)
        {
            // A lista de atributos de Face a serem retornados.
            IList<FaceAttributeType> faceAttributes =
                new FaceAttributeType[]
                {
                    FaceAttributeType.Gender, FaceAttributeType.Age,
                    FaceAttributeType.Smile, FaceAttributeType.Emotion,
                    FaceAttributeType.Glasses, FaceAttributeType.Hair
                };

            // Chame a API Face.
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    // O segundo argumento especifica o retorno do faceId, enquanto
                    // o terceiro argumento especifica para n�o retornar os pontos de refer�ncia do rosto.
                    IList<DetectedFace> faceList =
                        await faceClient.Face.DetectWithStreamAsync(
                            imageFileStream, true, false, faceAttributes);
                    return faceList;
                }
            }
            // Capture e exiba erros da API Face.
            catch (APIErrorException f)
            {
                MessageBox.Show(f.Message);
                return new List<DetectedFace>();
            }
            // Capture e exiba todos os outros erros.
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return new List<DetectedFace>();
            }
        }

        // Cria uma string com os atributos que descrevem o rosto.
        private string FaceDescription(DetectedFace face)
        {
            string sexo = "";

            if (face.FaceAttributes.Gender == Gender.Female)
            {
                sexo = "Mulher";
            }
            else
            {
                sexo = "Homem";
            }


            StringBuilder sb = new StringBuilder();

            sb.Append("           Resultado");
            sb.Append("\n");
            sb.Append("\n");

            sb.Append("Sexo: ");

            // Adicione o sexo, idade e sorriso.
            sb.Append(sexo);
            sb.Append("\n");
            sb.Append("\n");

            sb.Append("Idade: ");
            sb.Append(face.FaceAttributes.Age + " anos");
            sb.Append("\n");
            sb.Append("\n");
            sb.Append(String.Format("Sorrindo: {0:F1}% ", face.FaceAttributes.Smile * 100));
            sb.Append("\n");
            sb.Append("\n");

            // Adicione as emo��es. Exiba todas as emo��es acima de 10%.
            sb.Append("Emo��o: ");
            Emotion emotionScores = face.FaceAttributes.Emotion;
            if (emotionScores.Anger >= 0.1f) sb.Append(
                String.Format("Raiva {0:F1}% ", emotionScores.Anger * 100));
            if (emotionScores.Contempt >= 0.1f) sb.Append(
                String.Format("Desprezo {0:F1}% ", emotionScores.Contempt * 100));
            if (emotionScores.Disgust >= 0.1f) sb.Append(
                String.Format("Nojo {0:F1}% ", emotionScores.Disgust * 100));
            if (emotionScores.Fear >= 0.1f) sb.Append(
                String.Format("Medo {0:F1}% ", emotionScores.Fear * 100));
            if (emotionScores.Happiness >= 0.1f) sb.Append(
                String.Format("Felicidade {0:F1}% ", emotionScores.Happiness * 100));
            if (emotionScores.Neutral >= 0.1f) sb.Append(
                String.Format("Neutro {0:F1}% ", emotionScores.Neutral * 100));
            if (emotionScores.Sadness >= 0.1f) sb.Append(
                String.Format("Tristeza {0:F1}% ", emotionScores.Sadness * 100));
            if (emotionScores.Surprise >= 0.1f) sb.Append(
                String.Format("Surpresa {0:F1}% ", emotionScores.Surprise * 100));
            sb.Append("\n");
            sb.Append("\n");

            string olhos = "";

            if (face.FaceAttributes.Glasses == GlassesType.NoGlasses)
            {
                olhos = "Sem Oculos";
            }
            if (face.FaceAttributes.Glasses == GlassesType.ReadingGlasses)
            {
                olhos = "Com oculos de leitura";
            }
            if (face.FaceAttributes.Glasses == GlassesType.Sunglasses)
            {
                olhos = "Com oculos de sol";
            }
            if (face.FaceAttributes.Glasses == GlassesType.SwimmingGoggles)
            {
                olhos = "Com oculos de nata��o";
            }


            // Adicione os �culos.
            sb.Append(olhos);

            // Retorna a string constru�da.
            return sb.ToString();
        }
    }
}