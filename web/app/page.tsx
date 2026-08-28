import Image from "next/image";
import Link from "next/link";

const navigation = [
  { label: "About", href: "#about" },
  { label: "Skills", href: "#skills" },
  { label: "Experience", href: "#experience" },
  { label: "Projects", href: "#projects" },
  { label: "Contact", href: "#contact" },
];

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

const skillStyles = [
  "border-[#c084fc]/55 text-[#e9d5ff] hover:bg-[#c084fc]/10",
  "border-[#f472b6]/55 text-[#fbcfe8] hover:bg-[#f472b6]/10",
  "border-[#60a5fa]/55 text-[#bfdbfe] hover:bg-[#60a5fa]/10",
];

const experienceDetails = [
  "Exposure to BI reporting, dashboards, MIS platforms, and decision-support processes.",
  "Worked alongside IT Operations teams on enterprise infrastructure and support workflows.",
  "Explored automation and RPA applications with technical, operational, and business stakeholders.",
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

function SectionLabel({ children }: { children: React.ReactNode }) {
  return <p className="text-xs font-semibold uppercase tracking-[0.2em] text-white/48">{children}</p>;
}

function Scribble({ className = "" }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 240 90" fill="none" aria-hidden="true">
      <path
        d="M4 46C27 6 44 80 66 38C85 3 104 80 126 39C144 6 167 73 188 37C204 10 220 22 236 50"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
    </svg>
  );
}

function PortfolioNav() {
  return (
    <header className="mx-auto flex w-full max-w-[1280px] items-center justify-between px-6 py-7 sm:px-10 lg:px-14">
      <a href="#top" className="group flex items-center gap-3 outline-none focus-visible:ring-2 focus-visible:ring-[#c084fc]">
        <span className="grid h-10 w-10 place-items-center rounded-full border border-[#c084fc]/55 text-sm font-semibold text-white transition group-hover:bg-[#c084fc]/10">
          AA
        </span>
        <span className="text-xs font-semibold uppercase tracking-[0.22em] text-white/76">Ahmad Abughanam</span>
      </a>

      <nav aria-label="Portfolio navigation" className="hidden items-center gap-8 lg:flex">
        {navigation.map((item) => (
          <a key={item.href} href={item.href} className="text-xs font-semibold uppercase tracking-[0.14em] text-white/58 transition hover:text-white">
            {item.label}
          </a>
        ))}
        <Link
          href="/login"
          className="rounded-full border border-[#60a5fa]/55 px-5 py-2.5 text-xs font-semibold uppercase tracking-[0.14em] text-[#bfdbfe] transition hover:bg-[#60a5fa]/10"
        >
          Open Drive
        </Link>
      </nav>

      <details className="group relative lg:hidden">
        <summary className="flex h-10 w-10 cursor-pointer list-none flex-col items-end justify-center gap-2 rounded-full border border-white/20 px-2.5 outline-none focus-visible:ring-2 focus-visible:ring-[#c084fc] [&::-webkit-details-marker]:hidden" aria-label="Open portfolio navigation">
          <span className="h-px w-5 bg-white" />
          <span className="h-px w-3.5 bg-white transition group-open:w-5" />
        </summary>
        <nav className="absolute right-0 z-50 mt-3 w-52 rounded-[8px] border border-white/12 bg-[#111118] p-3 shadow-[0_18px_50px_rgba(0,0,0,0.55)]" aria-label="Mobile portfolio navigation">
          {navigation.map((item) => (
            <a key={item.href} href={item.href} className="block rounded-[6px] px-3 py-3 text-xs font-semibold uppercase tracking-[0.14em] text-white/70 hover:bg-white/[0.06] hover:text-white">
              {item.label}
            </a>
          ))}
          <Link href="/login" className="mt-2 block rounded-full border border-[#60a5fa]/55 px-3 py-3 text-center text-xs font-semibold uppercase tracking-[0.14em] text-[#bfdbfe]">
            Open Drive
          </Link>
        </nav>
      </details>
    </header>
  );
}

function HeroSection() {
  return (
    <section className="mx-auto grid w-full max-w-[1280px] items-center gap-14 px-6 pb-20 pt-8 sm:px-10 lg:min-h-[690px] lg:grid-cols-[1.08fr_0.92fr] lg:gap-16 lg:px-14 lg:pb-24 lg:pt-12">
      <div className="relative z-10 max-w-[720px]">
        <SectionLabel>Ahmad Maher Abughanam</SectionLabel>
        <h1 className="mt-6 text-balance text-[46px] font-semibold leading-[1.03] text-white sm:text-[64px] lg:text-[82px]">
          <span className="sr-only">Ahmad Maher Abughanam</span>
          <span aria-hidden="true">
            Engineering
            <br />
            <span className="bg-gradient-to-r from-[#c084fc] via-[#f472b6] to-[#60a5fa] bg-clip-text text-transparent">intelligent systems.</span>
          </span>
        </h1>
        <p className="mt-7 max-w-[610px] text-base leading-8 text-white/64 sm:text-lg">
          AI Engineer & Backend Software Engineer specializing in building scalable systems, automation, and intelligent applications.
        </p>

        <div className="mt-9 flex flex-wrap items-center gap-4">
          <a href="#projects" className="rounded-full bg-white px-6 py-3.5 text-xs font-semibold uppercase tracking-[0.16em] text-black transition hover:bg-[#e9d5ff]">
            Explore work
          </a>
          <Link href="/login" className="rounded-full border border-white/20 px-6 py-3.5 text-xs font-semibold uppercase tracking-[0.16em] text-white transition hover:border-[#f472b6]/60 hover:text-[#fbcfe8]">
            Enter Drive
          </Link>
        </div>

        <div className="mt-12 flex items-center gap-6 text-xs font-semibold uppercase tracking-[0.16em] text-white/52">
          <a href="https://github.com/AhmadAbughanam" className="transition hover:text-white">GitHub</a>
          <a href="https://linkedin.com/in/ahmad-maher" className="transition hover:text-white">LinkedIn</a>
          <span className="h-px w-14 bg-gradient-to-r from-[#c084fc] to-[#60a5fa]" aria-hidden="true" />
        </div>
      </div>

      <div className="relative mx-auto w-full max-w-[520px] lg:mr-0">
        <div className="absolute -left-5 top-12 h-24 w-px rotate-12 bg-[#f472b6]/65 sm:-left-10" aria-hidden="true" />
        <div className="absolute -right-3 bottom-16 h-px w-24 -rotate-12 bg-[#60a5fa]/70 sm:-right-8" aria-hidden="true" />
        <Scribble className="absolute -right-2 -top-9 z-10 w-32 text-[#c084fc] sm:w-44" />
        <div className="relative aspect-[4/5] overflow-hidden rounded-[8px] border border-white/12 bg-[#111118] shadow-[0_28px_90px_rgba(0,0,0,0.48)]">
          <Image
            src="/profile.png"
            alt="Black and white side-profile portrait of Ahmad Abughanam"
            fill
            priority
            sizes="(max-width: 1024px) 90vw, 42vw"
            className="object-cover object-[50%_52%] grayscale"
          />
          <div className="absolute inset-x-0 bottom-0 h-32 bg-gradient-to-t from-[#08080c] to-transparent" aria-hidden="true" />
          <div className="absolute bottom-5 left-5 right-5 flex items-center justify-between border-t border-white/18 pt-4 text-[11px] font-semibold uppercase tracking-[0.16em] text-white/66">
            <span>Amman, Jordan</span>
            <span>AI + Backend</span>
          </div>
        </div>
      </div>
    </section>
  );
}

function SkillsSection() {
  return (
    <section id="skills" className="border-y border-white/[0.08] bg-[#0b0b10] px-6 py-20 sm:px-10 lg:py-24">
      <div className="mx-auto max-w-[1120px] text-center">
        <SectionLabel>Skills</SectionLabel>
        <h2 className="mt-4 text-3xl font-semibold text-white sm:text-5xl">Technical Skills</h2>
        <Scribble className="mx-auto mt-5 w-40 text-[#c084fc]" />
        <div className="mx-auto mt-10 flex max-w-[940px] flex-wrap justify-center gap-3">
          {skills.map((skill, index) => (
            <span key={skill} className={`rounded-full border px-4 py-2.5 text-sm transition ${skillStyles[index % skillStyles.length]}`}>
              {skill}
            </span>
          ))}
        </div>
      </div>
    </section>
  );
}

function ExperienceSection() {
  return (
    <section id="experience" className="relative overflow-hidden bg-[#020203] px-6 py-20 sm:px-10 lg:py-28">
      <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-[#f472b6]/70 to-transparent" aria-hidden="true" />
      <div className="mx-auto max-w-[1180px]">
        <div className="grid gap-8 lg:grid-cols-[0.72fr_1.28fr] lg:gap-20">
          <div>
            <SectionLabel>Experience</SectionLabel>
            <h2 className="mt-4 text-4xl font-semibold leading-tight text-white sm:text-6xl">Banking IT and automation exposure.</h2>
          </div>

          <article className="relative border-t border-white/16 pt-8">
            <div className="flex flex-col gap-5 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[#60a5fa]">IT / Automation Intern</p>
                <h3 className="mt-3 text-3xl font-semibold text-white">Jordan Kuwait Bank</h3>
                <p className="mt-2 text-sm text-white/48">Amman, Jordan</p>
              </div>
              <span className="w-fit rounded-full border border-[#f472b6]/45 px-4 py-2 text-xs font-semibold uppercase tracking-[0.16em] text-[#fbcfe8]">3 months</span>
            </div>

            <div className="relative mt-12 pl-12 sm:pl-16">
              <svg className="absolute bottom-0 left-0 top-0 h-full w-10 overflow-visible sm:w-12" viewBox="0 0 48 360" preserveAspectRatio="none" fill="none" aria-hidden="true">
                <defs>
                  <linearGradient id="timeline-line" x1="6" y1="0" x2="42" y2="360" gradientUnits="userSpaceOnUse">
                    <stop stopColor="#c084fc" />
                    <stop offset="0.5" stopColor="#f472b6" />
                    <stop offset="1" stopColor="#60a5fa" />
                  </linearGradient>
                </defs>
                <path d="M24 0C4 44 44 78 22 120C3 158 44 196 22 236C4 275 41 309 24 360" stroke="url(#timeline-line)" strokeWidth="2" strokeLinecap="round" />
              </svg>

              <ol className="space-y-7">
                {experienceDetails.map((detail, index) => (
                  <li key={detail} className="relative rounded-[8px] border border-white/[0.09] bg-white/[0.035] p-5 text-sm leading-7 text-white/66 sm:p-6 sm:text-base">
                    <span className={`absolute -left-[43px] top-7 grid h-7 w-7 place-items-center rounded-full border bg-[#020203] text-[10px] font-semibold sm:-left-[59px] ${index === 0 ? "border-[#c084fc] text-[#e9d5ff]" : index === 1 ? "border-[#f472b6] text-[#fbcfe8]" : "border-[#60a5fa] text-[#bfdbfe]"}`}>
                      0{index + 1}
                    </span>
                    {detail}
                  </li>
                ))}
              </ol>
            </div>
          </article>
        </div>
      </div>
    </section>
  );
}

function AboutSection() {
  return (
    <section id="about" className="bg-[#0d0c13] px-6 py-20 sm:px-10 lg:py-28">
      <div className="mx-auto grid max-w-[1180px] items-center gap-12 lg:grid-cols-[0.88fr_1.12fr] lg:gap-20">
        <div className="relative mx-auto w-full max-w-[470px]">
          <div className="absolute -bottom-4 -left-4 h-full w-full rounded-[8px] border border-[#c084fc]/35" aria-hidden="true" />
          <div className="relative aspect-[5/6] overflow-hidden rounded-[8px] border border-white/12 bg-black">
            <Image src="/profile.png" alt="Portrait of Ahmad Abughanam" fill sizes="(max-width: 1024px) 90vw, 40vw" className="object-cover object-[48%_46%] grayscale" />
          </div>
          <Scribble className="absolute -bottom-12 right-0 w-36 text-[#60a5fa]" />
        </div>

        <div>
          <SectionLabel>About Me</SectionLabel>
          <h2 className="mt-4 max-w-[650px] text-4xl font-semibold leading-tight text-white sm:text-6xl">Backend systems, AI integration, and automation.</h2>
          <p className="mt-8 text-base leading-8 text-white/62 sm:text-lg">
            Computer Science and Artificial Intelligence graduate based in Amman, Jordan, focused on secure APIs,
            production-inspired engineering, intelligent applications, and scalable backend systems.
          </p>

          <dl className="mt-10 grid gap-8 border-t border-white/12 pt-8 sm:grid-cols-2">
            <div>
              <dt className="text-xs font-semibold uppercase tracking-[0.18em] text-[#f472b6]">Location</dt>
              <dd className="mt-3 text-base text-white/78">Amman, Jordan</dd>
            </div>
            <div>
              <dt className="text-xs font-semibold uppercase tracking-[0.18em] text-[#60a5fa]">Education</dt>
              <dd className="mt-3 text-base leading-7 text-white/78">B.Sc. Computer Science / Artificial Intelligence</dd>
              <dd className="mt-1 text-sm leading-6 text-white/48">Middle East University, July 2026, GPA 3.50 / 4.00</dd>
            </div>
          </dl>
        </div>
      </div>
    </section>
  );
}

function ProjectsSection() {
  return (
    <section id="projects" className="bg-[#060608] px-6 py-20 sm:px-10 lg:py-28">
      <div className="mx-auto max-w-[1180px]">
        <div className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <SectionLabel>Projects</SectionLabel>
            <h2 className="mt-4 text-4xl font-semibold text-white sm:text-6xl">Selected Work</h2>
          </div>
        </div>

        <article className="mt-14 grid items-center gap-14 border-t border-white/12 pt-12 lg:grid-cols-[1.08fr_0.92fr] lg:gap-20">
          <div className="grid gap-4 sm:relative sm:min-h-[520px]">
            <figure className="overflow-hidden rounded-[8px] border border-white/12 bg-[#101016] shadow-[0_24px_70px_rgba(0,0,0,0.42)] sm:absolute sm:left-0 sm:top-0 sm:w-[86%]">
              <Image src="/drive-files.png" alt="Amanah Drive file management dashboard" width={1440} height={900} className="h-auto w-full" />
            </figure>
            <figure className="overflow-hidden rounded-[8px] border border-[#c084fc]/35 bg-[#101016] shadow-[0_24px_70px_rgba(0,0,0,0.5)] sm:absolute sm:bottom-0 sm:right-0 sm:w-[76%]">
              <Image src="/drive-search-chat.png" alt="Amanah Drive semantic search and AI chat dashboard" width={1440} height={900} className="h-auto w-full" />
            </figure>
          </div>

          <div>
            <div className="flex items-center gap-4">
              <span className="text-sm font-semibold text-[#f472b6]">01</span>
              <span className="h-px w-16 bg-[#f472b6]/55" aria-hidden="true" />
            </div>
            <Link href="/login" aria-label="Amanah Drive" className="group mt-6 inline-flex items-center gap-5 outline-none focus-visible:ring-2 focus-visible:ring-[#c084fc]">
              <span className="text-4xl font-semibold text-white transition group-hover:text-[#e9d5ff] sm:text-6xl">Amanah Drive</span>
              <span className="text-3xl text-[#c084fc] transition group-hover:translate-x-1" aria-hidden="true">-&gt;</span>
            </Link>
            <p className="mt-7 max-w-[500px] text-base leading-8 text-white/62 sm:text-lg">
              A secure AI-powered storage & knowledge drive with semantic search and RAG.
            </p>
            <div className="mt-8 flex flex-wrap gap-2">
              {["ASP.NET Core", "FastAPI", "Next.js", "pgvector", "Docker"].map((item, index) => (
                <span key={item} className={`rounded-full border px-3 py-2 text-xs ${skillStyles[index % skillStyles.length]}`}>{item}</span>
              ))}
            </div>
            <Link href="/login" className="mt-9 inline-flex items-center gap-3 border-b border-white/30 pb-2 text-xs font-semibold uppercase tracking-[0.16em] text-white transition hover:border-[#60a5fa] hover:text-[#bfdbfe]">
              View case study <span aria-hidden="true">-&gt;</span>
            </Link>
          </div>
        </article>
      </div>
    </section>
  );
}

function CredentialsSection() {
  return (
    <section className="border-y border-white/[0.08] bg-[#0b0b10] px-6 py-20 sm:px-10 lg:py-28">
      <div className="mx-auto grid max-w-[1180px] gap-16 lg:grid-cols-[1fr_1fr] lg:gap-20">
        <div>
          <SectionLabel>Achievements</SectionLabel>
          <h2 className="mt-4 text-4xl font-semibold text-white sm:text-5xl">Achievements</h2>
          <ol className="mt-10 space-y-5">
            {achievements.map((achievement, index) => (
              <li key={achievement} className="grid grid-cols-[42px_1fr] gap-4 border-t border-white/12 pt-5 text-sm leading-7 text-white/62 sm:text-base">
                <span className={index === 0 ? "text-[#c084fc]" : index === 1 ? "text-[#f472b6]" : "text-[#60a5fa]"}>0{index + 1}</span>
                <span>{achievement}</span>
              </li>
            ))}
          </ol>
        </div>

        <div>
          <SectionLabel>Certifications</SectionLabel>
          <h2 className="mt-4 text-4xl font-semibold text-white sm:text-5xl">Certifications</h2>
          <div className="mt-10 flex flex-wrap gap-3">
            {certifications.map((item, index) => (
              <span key={item} className={`rounded-full border px-4 py-3 text-sm leading-5 ${skillStyles[index % skillStyles.length]}`}>{item}</span>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}

function ContactSection() {
  return (
    <section id="contact" className="relative overflow-hidden bg-[#050507] px-6 py-20 sm:px-10 lg:py-28">
      <Scribble className="absolute right-6 top-8 w-44 text-[#f472b6]/55 sm:right-16 sm:w-64" />
      <div className="relative mx-auto grid max-w-[1180px] gap-14 lg:grid-cols-[1fr_0.9fr] lg:gap-24">
        <form action="mailto:abughannam98@gmail.com" method="post" encType="text/plain" className="space-y-7">
          <div>
            <SectionLabel>Contact</SectionLabel>
            <h2 className="mt-4 text-4xl font-semibold text-white sm:text-6xl">Contact</h2>
          </div>
          <label className="block">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-white/48">Name</span>
            <input name="name" required className="mt-3 w-full border-0 border-b border-white/22 bg-transparent px-0 py-3 text-base text-white outline-none transition placeholder:text-white/24 focus:border-[#c084fc]" />
          </label>
          <label className="block">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-white/48">Email</span>
            <input name="email" type="email" required className="mt-3 w-full border-0 border-b border-white/22 bg-transparent px-0 py-3 text-base text-white outline-none transition placeholder:text-white/24 focus:border-[#f472b6]" />
          </label>
          <label className="block">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-white/48">Message</span>
            <textarea name="message" required rows={5} className="mt-3 w-full resize-y rounded-[8px] border border-white/18 bg-white/[0.025] p-4 text-base leading-7 text-white outline-none transition placeholder:text-white/24 focus:border-[#60a5fa]" />
          </label>
          <button type="submit" className="rounded-full bg-white px-6 py-3.5 text-xs font-semibold uppercase tracking-[0.16em] text-black transition hover:bg-[#e9d5ff]">
            Send message
          </button>
        </form>

        <div className="border-t border-white/14 pt-8 lg:border-l lg:border-t-0 lg:pl-14 lg:pt-0">
          <p className="text-sm leading-7 text-white/50">Direct contact</p>
          <div className="mt-7 space-y-5 text-2xl font-semibold leading-tight text-white sm:text-3xl">
            <a href="mailto:abughannam98@gmail.com" className="block break-words transition hover:text-[#fbcfe8]">abughannam98@gmail.com</a>
            <a href="tel:+962786099743" className="block transition hover:text-[#bfdbfe]">0786099743</a>
          </div>
          <div className="mt-10 flex flex-wrap gap-3">
            <a href="https://github.com/AhmadAbughanam" className="rounded-full border border-white/18 px-5 py-3 text-xs font-semibold uppercase tracking-[0.16em] text-white/70 transition hover:border-[#c084fc] hover:text-white">GitHub</a>
            <a href="https://linkedin.com/in/ahmad-maher" className="rounded-full border border-white/18 px-5 py-3 text-xs font-semibold uppercase tracking-[0.16em] text-white/70 transition hover:border-[#60a5fa] hover:text-white">LinkedIn</a>
          </div>
        </div>
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <main id="top" className="min-h-screen overflow-x-hidden bg-[#060608] text-white">
      <PortfolioNav />
      <HeroSection />
      <SkillsSection />
      <ExperienceSection />
      <AboutSection />
      <ProjectsSection />
      <CredentialsSection />
      <ContactSection />
      <footer className="border-t border-white/[0.08] bg-[#050507] px-6 py-7 sm:px-10">
        <div className="mx-auto flex max-w-[1180px] flex-col gap-3 text-xs uppercase tracking-[0.14em] text-white/38 sm:flex-row sm:items-center sm:justify-between">
          <span>Ahmad Maher Abughanam</span>
          <span>Amman, Jordan</span>
        </div>
      </footer>
    </main>
  );
}
