<p align="center">
  <img src="icon.svg" alt="MiniJeu AWS Logo" width="120" height="120">
</p>

<h1 align="center">🎮 MiniJeu Cartes AWS</h1>

<p align="center">
  <strong>Prépare ta certification AWS Cloud Practitioner en t'amusant !</strong>
</p>

<p align="center">
  <a href="https://github.com/Guilhem-Bonnet/MiniJeuCartesAWS/releases/latest">
    <img src="https://img.shields.io/github/v/release/Guilhem-Bonnet/MiniJeuCartesAWS?style=for-the-badge&color=FF9900" alt="Latest Release">
  </a>
  <img src="https://img.shields.io/badge/Questions-398-blue?style=for-the-badge" alt="Questions">
  <img src="https://img.shields.io/badge/Leçons-93-green?style=for-the-badge" alt="Leçons">
  <img src="https://img.shields.io/badge/Services_AWS-95+-orange?style=for-the-badge" alt="Services AWS">
</p>

<p align="center">
  <a href="#-téléchargement">Télécharger</a> •
  <a href="#-fonctionnalités">Fonctionnalités</a> •
  <a href="#-contenu-pédagogique">Contenu</a> •
  <a href="#-développement">Développement</a>
</p>

---

## 🚀 Téléchargement

<table>
  <tr>
    <td align="center"><b>🪟 Windows</b></td>
    <td align="center"><b>🐧 Linux</b></td>
    <td align="center"><b>🍎 macOS</b></td>
  </tr>
  <tr>
    <td align="center">
      <a href="https://github.com/Guilhem-Bonnet/MiniJeuCartesAWS/releases/latest/download/MiniJeuCartesAWS-windows-x86_64.zip">
        <img src="https://img.shields.io/badge/Download-Windows-0078D6?style=for-the-badge&logo=windows" alt="Download Windows">
      </a>
    </td>
    <td align="center">
      <a href="https://github.com/Guilhem-Bonnet/MiniJeuCartesAWS/releases/latest/download/MiniJeuCartesAWS-linux-x86_64.zip">
        <img src="https://img.shields.io/badge/Download-Linux-FCC624?style=for-the-badge&logo=linux&logoColor=black" alt="Download Linux">
      </a>
    </td>
    <td align="center">
      <a href="https://github.com/Guilhem-Bonnet/MiniJeuCartesAWS/releases/latest/download/MiniJeuCartesAWS-macos-universal.zip">
        <img src="https://img.shields.io/badge/Download-macOS-000000?style=for-the-badge&logo=apple" alt="Download macOS">
      </a>
    </td>
  </tr>
</table>

> **Note Linux** : Après extraction, rends le fichier exécutable avec `chmod +x MiniJeuCartesAWS.x86_64`

---

## ✨ Fonctionnalités

### 🎯 Apprentissage gamifié
- **Cartes 3D interactives** avec animations fluides
- **Mode Timed Run** : réponds à un maximum de questions en temps limité
- **Feedback immédiat** : explication détaillée après chaque réponse
- **Progression visuelle** : score, streak, meilleur score

### 📚 Contenu complet pour le CCP
- **398 questions** couvrant les 4 domaines de l'examen
- **93 leçons** pour apprendre chaque service AWS
- **95+ services AWS** : EC2, Lambda, S3, RDS, IAM, VPC, CloudFront, et bien plus
- **Questions pièges** réalistes basées sur l'examen officiel

### 🎮 3 niveaux de difficulté
| Niveau | Description |
|--------|-------------|
| 🟢 **Débutant** | Questions fondamentales pour commencer |
| 🟡 **Expert** | Scénarios plus complexes et comparaisons |
| 🔴 **Maître** | Questions pièges et cas d'usage avancés |

### ⚙️ Personnalisation
- Durée de run ajustable (1 à 10 minutes)
- Contrôle du volume (musique & effets)
- Menu pause avec accès aux cours
- Sauvegarde automatique des paramètres

---

## 📖 Contenu pédagogique

### Domaines couverts

| Domaine | Description | Questions |
|---------|-------------|-----------|
| ☁️ **Cloud Concepts** | Avantages du cloud, modèles de déploiement | ~60 |
| 🔒 **Security & Compliance** | IAM, chiffrement, conformité | ~100 |
| 💻 **Technology** | Services AWS, architectures | ~180 |
| 💰 **Billing & Pricing** | Modèles de tarification, outils de coûts | ~60 |

### Services AWS inclus

<details>
<summary><b>🖥️ Compute</b></summary>

EC2, Lambda, ECS, EKS, Fargate, Batch, Lightsail, Elastic Beanstalk, App Runner
</details>

<details>
<summary><b>💾 Storage</b></summary>

S3, EBS, EFS, FSx, S3 Glacier, Storage Gateway, Snowball, AWS Backup, DataSync
</details>

<details>
<summary><b>🗄️ Database</b></summary>

RDS, Aurora, DynamoDB, ElastiCache, Redshift, Neptune, DocumentDB
</details>

<details>
<summary><b>🌐 Networking</b></summary>

VPC, Route 53, CloudFront, ELB, Direct Connect, API Gateway, Route Tables, NACL
</details>

<details>
<summary><b>🔐 Security</b></summary>

IAM, KMS, Secrets Manager, WAF, Shield, GuardDuty, Inspector, Macie, Security Hub, Cognito, ACM
</details>

<details>
<summary><b>📊 Analytics & ML</b></summary>

Athena, QuickSight, Kinesis, Glue, EMR, SageMaker, Rekognition, Comprehend, Lex, Polly, Textract, Translate, Transcribe
</details>

<details>
<summary><b>🛠️ Management & DevOps</b></summary>

CloudWatch, CloudTrail, Config, Systems Manager, CloudFormation, CodeCommit, CodeBuild, CodeDeploy, CodePipeline, Organizations, Control Tower
</details>

<details>
<summary><b>💵 Billing</b></summary>

Cost Explorer, Budgets, Pricing Calculator, Savings Plans, Trusted Advisor, Support Plans
</details>

---

## 🛠️ Développement

### Prérequis

- [Godot 4.x Mono](https://godotengine.org/download) (avec support C#)
- [.NET 8 SDK](https://dotnet.microsoft.com/download)

### Lancer en local

```bash
# Cloner le repo
git clone https://github.com/Guilhem-Bonnet/MiniJeuCartesAWS.git
cd MiniJeuCartesAWS

# Build C#
dotnet build

# Ouvrir dans Godot
godot --path .
```

### Structure du projet

```
MiniJeuCartesAWS/
├── Data/
│   ├── questions_practitioner.json  # 398 questions
│   └── course_practitioner.json     # 93 leçons
├── Scenes/
│   └── Main3D.tscn                  # Scène principale
├── Scripts/
│   ├── TimedRunUI.cs                # Logique principale
│   ├── TimedRunUI.Deck.cs           # Gestion du deck
│   ├── TimedRunUI.Course.cs         # Système de cours
│   └── ...
└── Assets/                          # Ressources 3D, audio, fonts
```

### Documentation

- [Direction artistique](DA.md)
- [Règles du jeu](REGLES_DU_JEU.md)
- [Roadmap](ROADMAP.md)
- [Guide IA/Architecture](IA_CONTEXT.md)

---

## 🤝 Contribution

Les contributions sont les bienvenues ! Tu peux :

- 🐛 Signaler des bugs via les [Issues](https://github.com/Guilhem-Bonnet/MiniJeuCartesAWS/issues)
- 💡 Proposer de nouvelles questions ou corrections
- 🔧 Soumettre des Pull Requests

---

## 📜 Licence

Ce projet est sous licence MIT. Voir le fichier [LICENSE](LICENSE) pour plus de détails.

---

## 🙏 Crédits

- Développé avec [Godot Engine](https://godotengine.org/) 4.x Mono
- Inspiré par l'examen [AWS Certified Cloud Practitioner](https://aws.amazon.com/certification/certified-cloud-practitioner/)

---

<p align="center">
  <b>⭐ Si ce projet t'aide dans ta préparation, n'hésite pas à lui donner une étoile !</b>
</p>

<p align="center">
  <a href="https://github.com/Guilhem-Bonnet/MiniJeuCartesAWS">
    <img src="https://img.shields.io/github/stars/Guilhem-Bonnet/MiniJeuCartesAWS?style=social" alt="GitHub stars">
  </a>
</p>
