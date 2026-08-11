import Link from "next/link";

const skills = [
  "Python",
  "C#",
  "Go",
  "TypeScript",
  "ASP.NET Core",
  "FastAPI",
  "Next.js",
  "PostgreSQL",
  "pgvector",
  "Docker",
  "GitHub Actions",
  "RAG",
  "LLMs",
  "Semantic search",
  "RabbitMQ",
  "Kafka fundamentals",
];

const certifications = [
  "Fine-Tuning Language Models",
  "Fundamentals of Large Language Models",
  "LLM Engineering",
  "Prompt Engineering",
  "Docker Mastery with Kubernetes and Swarm",
  "AWS Cloud Fundamentals",
  "Cisco CCNA",
  "Oracle Database SQL",
];

export default function Home() {
  return (
    <main className="min-h-screen bg-[#f7f8fb]">
      <section className="border-b border-slate-200 bg-white">
        <div className="mx-auto grid max-w-6xl gap-10 px-5 py-12 md:grid-cols-[1.3fr_0.7fr] md:px-8 md:py-16">
          <div className="flex flex-col justify-center">
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-teal-700">AI Engineer / Backend Software Engineer</p>
            <h1 className="mt-4 max-w-3xl text-4xl font-semibold leading-tight text-slate-950 md:text-6xl">
              Ahmad Maher Abughanam
            </h1>
            <p className="mt-5 max-w-2xl text-lg leading-8 text-slate-700">
              Computer Science and Artificial Intelligence graduate based in Amman, Jordan, focused on backend systems,
              AI integrations, secure APIs, automation, and production-inspired engineering.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                href="/login"
                className="inline-flex items-center justify-center rounded-md bg-slate-950 px-5 py-3 text-sm font-semibold text-white transition hover:bg-teal-800"
              >
                Amanah Drive
              </Link>
              <a
                href="https://github.com/AhmadAbughanam"
                className="inline-flex items-center justify-center rounded-md border border-slate-300 px-5 py-3 text-sm font-semibold text-slate-900 transition hover:border-teal-700 hover:text-teal-800"
              >
                GitHub
              </a>
              <a
                href="https://linkedin.com/in/ahmad-maher"
                className="inline-flex items-center justify-center rounded-md border border-slate-300 px-5 py-3 text-sm font-semibold text-slate-900 transition hover:border-teal-700 hover:text-teal-800"
              >
                LinkedIn
              </a>
            </div>
          </div>
          <aside className="rounded-lg border border-slate-200 bg-slate-50 p-6">
            <dl className="space-y-5 text-sm">
              <div>
                <dt className="font-semibold text-slate-950">Location</dt>
                <dd className="mt-1 text-slate-700">Amman, Jordan</dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-950">Education</dt>
                <dd className="mt-1 text-slate-700">B.Sc. Computer Science / Artificial Intelligence, Middle East University</dd>
                <dd className="text-slate-600">Graduated July 2026, GPA 3.50 / 4.00</dd>
              </div>
              <div>
                <dt className="font-semibold text-slate-950">Contact</dt>
                <dd className="mt-1 text-slate-700">abughannam98@gmail.com</dd>
                <dd className="text-slate-700">0786099743</dd>
              </div>
            </dl>
          </aside>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-5 py-12 md:px-8">
        <div className="grid gap-8 lg:grid-cols-[0.9fr_1.1fr]">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.16em] text-teal-700">Featured project</p>
            <h2 className="mt-3 text-3xl font-semibold text-slate-950">Amanah Drive</h2>
            <p className="mt-4 leading-7 text-slate-700">
              A secure AI-powered personal knowledge drive built as a production-inspired full-stack system. It combines
              ASP.NET Core, FastAPI, PostgreSQL with pgvector, local file storage, background document processing,
              semantic search, and grounded RAG chat.
            </p>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            {["JWT and refresh-token authentication", "Folder and file management", "PDF, Markdown, and text processing", "Embeddings, pgvector, search, and RAG"].map((item) => (
              <div key={item} className="rounded-lg border border-slate-200 bg-white p-5">
                <p className="font-medium text-slate-950">{item}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="border-y border-slate-200 bg-white">
        <div className="mx-auto max-w-6xl px-5 py-12 md:px-8">
          <div className="grid gap-10 md:grid-cols-[0.7fr_1.3fr]">
            <div>
              <p className="text-sm font-semibold uppercase tracking-[0.16em] text-teal-700">Experience</p>
              <h2 className="mt-3 text-3xl font-semibold text-slate-950">Banking IT and automation exposure</h2>
            </div>
            <div className="rounded-lg border border-slate-200 bg-slate-50 p-6">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h3 className="text-xl font-semibold text-slate-950">Jordan Kuwait Bank</h3>
                  <p className="text-sm text-slate-600">IT / Automation Intern, Amman, Jordan</p>
                </div>
                <p className="text-sm font-medium text-slate-700">3 months</p>
              </div>
              <ul className="mt-5 space-y-3 text-sm leading-6 text-slate-700">
                <li>Gained exposure to BI reporting, dashboards, MIS platforms, and decision-support processes in a regulated banking environment.</li>
                <li>Worked alongside IT Operations teams, learning enterprise infrastructure, technical-support workflows, and operational processes.</li>
                <li>Explored automation and RPA applications while collaborating with technical, operational, and business stakeholders.</li>
              </ul>
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-5 py-12 md:px-8">
        <div className="grid gap-10 lg:grid-cols-2">
          <div>
            <h2 className="text-3xl font-semibold text-slate-950">Technical skills</h2>
            <div className="mt-5 flex flex-wrap gap-2">
              {skills.map((skill) => (
                <span key={skill} className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-800">
                  {skill}
                </span>
              ))}
            </div>
          </div>
          <div>
            <h2 className="text-3xl font-semibold text-slate-950">Achievements and learning</h2>
            <ul className="mt-5 space-y-3 text-sm leading-6 text-slate-700">
              <li>2nd Place, SourceHive Hackathon: built an AI-powered resume intake system, HR dashboard, and interview portal.</li>
              <li>7th Place, IEEE AI Hackathon: ranked among leading participants in an AI-focused competition.</li>
              <li>Graduation project: full marks and A grade in both Graduation Project I and II for an AI-focused project.</li>
            </ul>
            <div className="mt-5 flex flex-wrap gap-2">
              {certifications.map((item) => (
                <span key={item} className="rounded-md bg-slate-100 px-3 py-2 text-xs text-slate-700">
                  {item}
                </span>
              ))}
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}
