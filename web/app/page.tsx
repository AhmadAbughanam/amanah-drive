import Image from "next/image";
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

const achievements = [
  "2nd Place, SourceHive Hackathon: built an AI-powered resume intake system, HR dashboard, and interview portal.",
  "7th Place, IEEE AI Hackathon: ranked among leading participants in an AI-focused competition.",
  "Graduation project: full marks and A grade in both Graduation Project I and II for an AI-focused project.",
];

export default function Home() {
  return (
    <main className="min-h-screen bg-[#080808] px-3 py-7 text-[#0b0b0b] md:px-8 md:py-10">
      <div className="mx-auto max-w-[430px] md:max-w-[1280px]">
        <div className="overflow-hidden rounded-[48px] border-[9px] border-[#151515] bg-[#090909] shadow-[0_18px_60px_rgba(0,0,0,0.65)] md:rounded-[18px] md:border-0">
          <section className="relative flex min-h-[700px] flex-col overflow-hidden bg-[#f7f7f5] px-9 pt-10 md:block md:min-h-[780px] md:px-24 md:pt-12">
            <header className="relative z-20 flex items-center justify-between">
              <a href="#about" className="font-serif text-[46px] leading-none text-black md:text-[54px]" aria-label="Ahmad Abughanam">
                AA
              </a>
              <nav aria-label="Portfolio navigation" className="hidden items-center gap-11 text-xs font-semibold uppercase md:flex">
                <a href="#about" className="transition hover:text-black/60">
                  About
                </a>
                <a href="#work" className="transition hover:text-black/60">
                  Work
                </a>
                <a href="#experience" className="transition hover:text-black/60">
                  Experience
                </a>
                <a href="#contact" className="transition hover:text-black/60">
                  Contact
                </a>
                <span className="h-2 w-2 rounded-full bg-black" aria-hidden="true" />
              </nav>
              <div className="flex h-9 w-9 flex-col items-end justify-center gap-2 md:hidden" aria-hidden="true">
                <span className="h-px w-6 bg-black" />
                <span className="h-px w-4 bg-black" />
              </div>
            </header>

            <div className="relative z-10 mt-20 max-w-[290px] md:mt-36 md:max-w-[670px]">
              <p className="text-sm font-semibold uppercase text-black md:text-base">Ahmad Abughanam</p>
              <h1 className="mt-7 font-serif text-[38px] font-normal leading-[1.12] text-black md:text-[64px] md:leading-[1.13]">
                <span className="sr-only">Ahmad Maher Abughanam</span>
                <span aria-hidden="true">
                  Engineering
                  <br />
                  Intelligent <em className="italic">Solutions.</em>
                </span>
              </h1>
              <div className="mt-8 h-px w-14 bg-black/80 md:mt-10" />
              <p className="mt-7 max-w-[300px] text-[15px] leading-7 text-black/72 md:mt-11 md:text-base">
                AI Engineer & Backend Software Engineer specializing in building scalable systems, automation, and intelligent applications.
              </p>
              <a href="#work" className="mt-8 inline-flex flex-col gap-2 text-sm font-semibold uppercase text-black md:mt-10">
                <span className="flex items-center gap-4">
                  Explore Work <span aria-hidden="true">-&gt;</span>
                </span>
                <span className="h-px w-16 bg-black" />
              </a>
            </div>

            <Image
              src="/profile.png"
              alt="Black and white side-profile portrait of Ahmad Abughanam"
              width={900}
              height={1100}
              priority
              className="relative z-0 mt-10 ml-auto h-[300px] w-[290px] object-cover object-[54%_44%] grayscale md:absolute md:right-[-20px] md:bottom-0 md:mt-0 md:h-[620px] md:w-[560px] md:object-[50%_48%]"
            />
          </section>

          <section id="work" className="bg-[#080808] px-9 py-8 text-white md:px-24 md:py-10">
            <div className="flex items-center justify-between text-xs font-semibold uppercase">
              <p>Selected Work</p>
              <p className="md:hidden">01 / 04</p>
              <div className="hidden items-center gap-9 text-2xl font-light md:flex" aria-hidden="true">
                <span>&lt;</span>
                <span>&gt;</span>
              </div>
            </div>
            <div className="mt-5 h-px bg-white/25 md:mt-7" />

            <div className="grid gap-7 pt-7 md:grid-cols-[90px_1fr_420px] md:items-start md:gap-12 md:pt-14">
              <div className="hidden md:block">
                <p className="text-sm">01</p>
                <div className="mt-3 h-px w-20 bg-white/40" />
              </div>
              <Link href="/login" className="group flex items-center justify-between gap-4 md:justify-start md:gap-8" aria-label="Amanah Drive">
                <span className="font-serif text-[27px] leading-none md:text-[40px]">Amanah Drive</span>
                <span className="text-3xl leading-none transition group-hover:translate-x-1" aria-hidden="true">
                  -&gt;
                </span>
              </Link>
              <div className="max-w-[340px] text-sm leading-7 text-white/70 md:max-w-none">
                <p>A secure AI-powered storage & knowledge drive with semantic search and RAG.</p>
                <Link href="/login" className="mt-6 inline-flex text-xs font-semibold uppercase text-white transition hover:text-white/70">
                  View Case Study
                </Link>
              </div>
            </div>
          </section>

          <section id="about" className="bg-[#f7f7f5] px-9 py-12 md:px-24 md:py-16">
            <div className="grid gap-10 md:grid-cols-[0.95fr_1.25fr] md:gap-20">
              <div>
                <p className="text-xs font-semibold uppercase text-black/60">About</p>
                <h2 className="mt-4 font-serif text-3xl font-normal leading-tight md:text-5xl">Backend systems, AI integration, and automation.</h2>
              </div>
              <div className="space-y-6 text-sm leading-7 text-black/70 md:text-base">
                <p>
                  Computer Science and Artificial Intelligence graduate based in Amman, Jordan, focused on secure APIs,
                  production-inspired engineering, intelligent applications, and scalable backend systems.
                </p>
                <dl className="grid gap-5 border-t border-black/15 pt-6 sm:grid-cols-2">
                  <div>
                    <dt className="text-xs font-semibold uppercase text-black/50">Location</dt>
                    <dd className="mt-2 text-black">Amman, Jordan</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-semibold uppercase text-black/50">Education</dt>
                    <dd className="mt-2 text-black">B.Sc. Computer Science / Artificial Intelligence</dd>
                    <dd>Middle East University, July 2026, GPA 3.50 / 4.00</dd>
                  </div>
                </dl>
              </div>
            </div>
          </section>

          <section id="experience" className="bg-[#101010] px-9 py-12 text-white md:px-24 md:py-16">
            <div className="grid gap-9 md:grid-cols-[0.6fr_1.4fr] md:gap-20">
              <div>
                <p className="text-xs font-semibold uppercase text-white/55">Experience</p>
                <h2 className="mt-4 font-serif text-3xl font-normal leading-tight md:text-5xl">Banking IT and automation exposure.</h2>
              </div>
              <div className="border-t border-white/20 pt-6">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <h3 className="font-serif text-2xl font-normal">Jordan Kuwait Bank</h3>
                    <p className="mt-1 text-sm text-white/58">IT / Automation Intern, Amman, Jordan</p>
                  </div>
                  <p className="text-sm text-white/72">3 months</p>
                </div>
                <ul className="mt-7 grid gap-4 text-sm leading-7 text-white/68 md:grid-cols-3">
                  <li>Exposure to BI reporting, dashboards, MIS platforms, and decision-support processes.</li>
                  <li>Worked alongside IT Operations teams on enterprise infrastructure and support workflows.</li>
                  <li>Explored automation and RPA applications with technical, operational, and business stakeholders.</li>
                </ul>
              </div>
            </div>
          </section>

          <section className="bg-[#f7f7f5] px-9 py-12 md:px-24 md:py-16">
            <div className="grid gap-12 lg:grid-cols-[1fr_1fr] lg:gap-20">
              <div>
                <p className="text-xs font-semibold uppercase text-black/55">Technical Skills</p>
                <div className="mt-7 flex flex-wrap gap-x-5 gap-y-3 text-sm leading-6 text-black/72">
                  {skills.map((skill) => (
                    <span key={skill} className="border-b border-black/25 pb-1">
                      {skill}
                    </span>
                  ))}
                </div>
              </div>
              <div>
                <p className="text-xs font-semibold uppercase text-black/55">Achievements</p>
                <ul className="mt-7 space-y-4 text-sm leading-7 text-black/72">
                  {achievements.map((achievement) => (
                    <li key={achievement}>{achievement}</li>
                  ))}
                </ul>
              </div>
            </div>
          </section>

          <section id="contact" className="bg-[#080808] px-9 py-12 text-white md:px-24 md:py-16">
            <div className="grid gap-10 md:grid-cols-[0.9fr_1.1fr] md:gap-20">
              <div>
                <p className="text-xs font-semibold uppercase text-white/55">Certifications</p>
                <div className="mt-7 grid gap-3 text-sm text-white/70 sm:grid-cols-2">
                  {certifications.map((item) => (
                    <p key={item}>{item}</p>
                  ))}
                </div>
              </div>
              <div>
                <p className="text-xs font-semibold uppercase text-white/55">Contact</p>
                <div className="mt-7 space-y-3 font-serif text-3xl leading-tight md:text-5xl">
                  <a href="mailto:abughannam98@gmail.com" className="block transition hover:text-white/70">
                    abughannam98@gmail.com
                  </a>
                  <a href="tel:+962786099743" className="block transition hover:text-white/70">
                    0786099743
                  </a>
                </div>
                <div className="mt-9 flex gap-6 text-sm font-semibold uppercase">
                  <a href="https://github.com/AhmadAbughanam" className="transition hover:text-white/70">
                    GitHub
                  </a>
                  <a href="https://linkedin.com/in/ahmad-maher" className="transition hover:text-white/70">
                    LinkedIn
                  </a>
                </div>
              </div>
            </div>
          </section>
        </div>
      </div>
    </main>
  );
}
