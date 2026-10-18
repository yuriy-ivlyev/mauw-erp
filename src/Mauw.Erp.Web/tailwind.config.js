/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        primary: '#3C50E0',
        secondary: '#80CAEE',
        dark: '#1F2937',
      },
    },
  },
  plugins: [],
  darkMode: 'class',
}
