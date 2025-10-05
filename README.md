
# 概要

**KinectEx**は、Microsoft の Kinect for Windows Unity Pro アドイン（v2.0.1410）を拡張し、Kinect V2 を使った Unity 開発をより手軽にするライブラリです。

KinectEx の機能を実演するサンプルプロジェクトを別パッケージとして提供しています。

&nbsp;
### KinectExができること

元のマイクロソフトのアドインでは、カメラ映像、距離センサー、骨格認識の3つが別々のサンプルとして提供されていました。そのためこれらを組み合わせて使用するには、専用のスクリプトを開発する必要がありました。

KinectEx は、これら3つのデータを統合した形で提供します：

- **背景の透過処理**: 人物だけを映像から切り抜き、背景を透明にした状態で出力します
- **骨格の位置調整**: 人物の映像にピッタリ重なるように、骨格トラッキングの位置を調整済みの状態で配置します
- **すぐに使用可能**: Prefab を配置するだけで、映像と骨格を組み合わせたデータをすぐに利用できます

センシング周りの実装を自作する必要がなく、作品固有の機能開発に集中できるため、インタラクティブアートやゲームの制作が容易になります。

**注意**: 本ライブラリは、映像とボーンを平面的に重ね合わせる2D出力に特化しています。Kinect V2 が持つ奥行き（Z軸）の情報は使用されないため、前後方向への動作検出には対応していません。

<img width="1124" height="627" alt="Image" src="https://github.com/user-attachments/assets/3bb69229-a85c-48b9-b50b-258cfb77359c" />

&nbsp;
### 主な機能

- カメラ映像、距離センサー、骨格トラッキングを統合して出力
- 最大6人の同時認識をサポート（Kinect V2 の仕様による制限）
- Unity の Built-in および URP レンダーパイプラインに対応
- Unity 初心者でも扱いやすい設計

&nbsp;
### 必要な環境

- **OS**: Windows のみ
- **センサー**: Kinect V2
- **Unity**: 2022.3.x（Built-in または URP）
- **依存関係**:
    - Kinect for Windows SDK 2.0（PC へのインストールが必要）
    - Kinect for Windows Unity Pro Add-in v2.0.1410（再配布制限のため本リポジトリには含まれていません）


&nbsp;
### ライセンス

KinectEx は MIT ライセンスの下で公開されています。なお、必要となる Microsoft Kinect SDK および Unity アドインは、独自のライセンスおよび再配布制限があります。

&nbsp;
### Git URL

#### KinectExライブラリ
```
https://github.com/tachu105/KinectEx.git?Assets/KinectEx
```

#### サンプルプロジェクト
```
https://github.com/tachu105/KinectEx.git?Assets/KinectEx.Sample
```

&nbsp;

&nbsp;
# 使用方法

Prefabs フォルダに格納されている **KinectBoneCutoutView** を Hierarchy に配置するだけで、すぐに利用できます。

<img width="249" height="131" alt="Image" src="https://github.com/user-attachments/assets/46b66664-dcc7-4d0c-8d7a-7fe3547252aa" />

KinectBoneCutoutView オブジェクトの **BodyCutoutViewController** コンポーネントにて、各種設定が可能です。

<img width="432" height="365" alt="Image" src="https://github.com/user-attachments/assets/3513d559-4175-4d43-ba82-6f6c4bbb8f3c" />

&nbsp;
### 設定項目

#### 映像反転設定
- **Mirror**: 映像とBoneを左右反転します
- **Flip**: 映像とBoneを上下反転します
#### 人物認識範囲設定
- **Max Distance**: センサーから人物を認識する最大距離（メートル単位）を設定します
- **Visualize Capture Distance**: 認識範囲内にある全てのオブジェクトを表示します。通常は人物の輪郭のみを抽出しますが、有効にすると認識範囲の確認が容易になります
#### 切り抜き精度設定
- **Downsample**: 映像の切り抜き精度を調整します。値が小さいほど精度が高く、値が大きいほど処理負荷が軽減されます
#### Bone表示設定
- **Bone Visible**: 認識しているBoneを表示します。非表示にしても当たり判定は有効です
- **Bone Pos Offset**: 映像とBoneの位置のズレを補正します
- **Bone Material**: Boneのジョイントに適用するマテリアルを設定します
- **Joint Base Size**: Boneのジョイントの大きさを設定します。当たり判定のサイズも連動して変動します
- **Line Base Width**: Boneのラインの太さを設定します

&nbsp;
### 注意事項

- **CutoutBodyByDepth** コンポーネントと **BoneMapper** コンポーネントは、機能の動作に必須です。削除や無効化を行うとエラーが発生します
- 外部クラスからパラメータ値を変更した場合、変更を反映するには `BodyCutoutViewController.ApplySettingsToComponents()` を呼び出してください



&nbsp;

&nbsp;
# 導入方法

### 1. Kinect For Windows SDK 2.0 のインストール

MicrosoftのSDKをインストールします。Kinectの開発に必須のソフトウェアです。  

1) 以下のリンクから、マイクロソフトの公式ダウンロードページを開き、「Kinect for Windows SDK 2.0」をダウンロードします。

   **ダウンロードページ**：[https://www.microsoft.com/en-us/download/details.aspx?id=44561](https://www.microsoft.com/en-us/download/details.aspx?id=44561)  
   ※ 上記がリンク切れの場合は[アーカイブページ](https://web.archive.org/web/20241203091151/https://learn.microsoft.com/en-us/windows/apps/design/devices/kinect-for-windows#:~:text=Get%20the%20Kinect%20for%20Windows%20SDK)の「Get the Kinect for Windows SDK」からダウンロードしてください。（ロードが遅いです）

2) ダウンロードしたexeファイルを実行し、インストールを行います。

&nbsp;
### 2. Unity用アドインのインポート

KinectをUnityの開発するために必要なベースとなるライブラリをインポートします。

1) 以下のリンクをクリックし、「KinectForWindows_UnityPro_2.0.1410.zip」をダウンロードします。

   **ダウンロードリンク**：[https://go.microsoft.com/fwlink/?LinkID=513177](https://go.microsoft.com/fwlink/?LinkID=513177)  
   ※ 上記がリンク切れの場合は[アーカイブページ](https://web.archive.org/web/20241203091151/https://learn.microsoft.com/en-us/windows/apps/design/devices/kinect-for-windows#:~:text=apps.%0ANuGet%20packages-,Unity%20Pro%20packages,-Kinect%20for%20Windows)の「Unity Pro packages」からダウンロードしてください。（ロードが遅いです）

3) zipを展開し、「Kinect.2.0.1410.19000.unitypackage」ファイルをUnityエディタのProjectタブ上にドラッグ&ドロップします。

4) 開いたImport UnityPackageウィンドウで、全ての項目にチェックが入っていることを確認し、右下「Import」ボタンを押します。

5) Assetsフォルダ下に「Plugins」フォルダと「Standard Assets」フォルダが追加されればインポート完了です。

&nbsp;
### 3. KinectExライブラリのインポート

本ライブラリはGitURLを用いた方法と、Unitypackageファイルを用いる方法の2種類が用意されています。  
好きな手法を選択してインポートしてください。

&nbsp;
### 3-1. Git URLを用いた方法

#### はじめに
GitURLによるパッケージのインポートを行うには、PCにGitがインストールされている必要があります。  
コマンドプロンプトにて ```git --version```と入力し、「git version ○.○○.○.windows.○」などとバージョンが表示されればインストール済です。  
その他のメッセージが表示される場合は未インストールですので、Gitの公式ページからインストールを行ってください。  

**Git ダウンロードリンク**：[https://git-scm.com/](https://git-scm.com/)

#### インポート手順
1) Unityエディタの上部メニュー「Window」から「PackageManager」を起動します。

2) 開いたPackage Managerウィンドウの左上にある「＋」ボタンをクリックし、「Add package from git URL...」を選択します。  
   ※ Unity6以降では「Install package from git URL...」に表記が変更されています。

3) ポップアップした入力欄に、以下のリンクを入力して、右側の「Add」ボタンをクリックします。

```
https://github.com/tachu105/KinectEx.git?path=Assets/KinectEx
```

4) Packages一覧に「KinectEx」が追加されればインポート完了です。  
   ※ ライブラリはPackagesフォルダ下に追加されます。Assetsフォルダではありませんので注意してください。

&nbsp;
### 3-2. UnityPackagesを用いた方法

#### インポート手順
1) 以下のリンクをクリックし、Releases画面に移動します。
   **Releaseページ**：[https://github.com/tachu105/KinectEx/releases](https://github.com/tachu105/KinectEx/releases)

2) 最新バージョン(Latest)の「Assets」の項目を開き、KinectEx.○.○.○.unitypackageを選択してファイルをダウンロードします。

3) UnityエディタのProjectタブ上にダウンロードしたファイルをドラッグ&ドロップします。

4) 開いたImport UnityPackageウィンドウで、必要な項目（基本全て）にチェックが入っていることを確認し、右下「Import」ボタンを押します。

5) Assetsフォルダ下に「KinectEx」フォルダが追加されればインポート完了です。



&nbsp;

&nbsp;
# サンプルプロジェクト

KinectExを用いた簡単なインタラクティブアートのサンプルデータを配布しています。

&nbsp;
### 作品概要

画面上の魔法陣に手で触れると、魔法陣が消滅します。  
※ 消滅時に音がなるので注意してください

<img width="1115" height="618" alt="Image" src="https://github.com/user-attachments/assets/11fddaf2-77bc-41d4-9ac2-ef0cbc5862e2" />

&nbsp;
### 導入方法

サンプルの導入方法としてGitURL、Unitypackage、本レポジトリの3種類を用意しています。  
詳細な導入手順は、「3. KinectExライブラリのインポート」と同じですので、そちらの操作を参考にしてください。

インポートが完了すると、Assetフォルダ下に「KinectEx.Sample」フォルダが追加されます。  
サンプルシーンは「KinectEx.Sample/Scenes/KinectExSample.scene」に格納されています。

#### Git URL
```
https://github.com/tachu105/KinectEx.git?Assets/KinectEx.Sample
```
※ サンプルを遊ぶためには、別途でKinect for Windows SDK 2.0、KinectForWindows_UnityPro、KinectExが必要です。

#### Unitypackage
Releasesページの「KinectEx.Sample.unitypackage」ファイルをダウンロードして、Unityにインポートしてください。  
※ サンプルを遊ぶためには、別途でKinect for Windows SDK 2.0、KinectForWindows_UnityPro、KinectExが必要です。

#### レポジトリ
本レポジトリをローカル環境にCloneしてください。  
※ サンプルを遊ぶためには、別途でKinect for Windows SDK 2.0、KinectForWindows_UnityProが必要です。


&nbsp;
### 権利表記
#### UnityEffectSample
Copyright (c) 2020 ktk.kumamoto

以下に定める条件に従い、本ソフトウェアおよび関連文書のファイル（以下「ソフトウェア」）の複製を取得するすべての人に対し、ソフトウェアを無制限に扱うことを無償で許可します。これには、ソフトウェアの複製を使用、複写、変更、結合、掲載、頒布、サブライセンス、および/または販売する権利、およびソフトウェアを提供する相手に同じことを許可する権利も無制限に含まれます。

上記の著作権表示および本許諾表示を、ソフトウェアのすべての複製または重要な部分に記載するものとします。

ソフトウェアは「現状のまま」で、明示であるか暗黙であるかを問わず、何らの保証もなく提供されます。ここでいう保証とは、商品性、特定の目的への適合性、および権利非侵害についての保証も含みますが、それに限定されるものではありません。 作者または著作権者は、契約行為、不法行為、またはそれ以外であろうと、ソフトウェアに起因または関連し、あるいはソフトウェアの使用またはその他の扱いによって生じる一切の請求、損害、その他の義務について何らの責任も負わないものとします。
